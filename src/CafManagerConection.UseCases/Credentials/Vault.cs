using System.Security.Cryptography;
using System.Text;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Credentials;

public enum ComoAbre
{
    /// <summary>No hay clave maestra: los secretos están en claro y se leen sin preguntar nada.</summary>
    SinClaveMaestra,

    ConLaClaveMaestra,
}

public sealed class VaultCerradoException : Exception
{
    public VaultCerradoException()
        : base("El vault está cerrado: hace falta la clave maestra para leer o guardar una contraseña.")
    {
    }
}

/// <summary>La fila del vault no pasa <see cref="FilaDelVault.PorQueNoSeUsa"/>. No tiene que ver con la clave maestra: una clave equivocada devuelve <see cref="ResultadoDeApertura.ClaveIncorrecta"/>.</summary>
public sealed class VaultDanadoException : Exception
{
    public VaultDanadoException(string motivo) : base(motivo)
    {
    }
}

/// <summary>Un secreto guardado no se descifra con la clave derivada actual. Los demás sí: es esta fila la que quedó cifrada con otra clave. No es recuperable y hay que volver a cargarla.</summary>
public sealed class CredencialIlegibleException : Exception
{
    public CredencialIlegibleException(ReferenciaDeSecreto referencia)
        : base($"La contraseña guardada para {referencia} no se puede descifrar con la clave "
               + "maestra de este vault, así que quedó cifrada con otra. No hay forma de "
               + "recuperarla: hay que volver a cargarla.")
    {
        Referencia = referencia;
    }

    public ReferenciaDeSecreto Referencia { get; }
}

/// <summary>Resultado de intentar abrir con la clave maestra, para que quien pregunta pueda decir la verdad en lugar de «no abrió».</summary>
public enum ResultadoDeApertura
{
    Abierto,
    ClaveIncorrecta,

    /// <summary>Este vault no tiene clave maestra: no hay nada que tipear.</summary>
    NoTieneClaveMaestra,
}

/// <summary>
/// Los secretos de la aplicación. La clave maestra es OPCIONAL y es lo único que protege: sin ella
/// el secreto se guarda en claro y la base abre en cualquier equipo.
/// </summary>
public sealed class Vault : IDisposable
{
    private readonly IRepositorioDelVault _repositorio;
    private readonly SemaphoreSlim _candado = new(1, 1);

    private byte[] _claveDerivada = [];
    private bool _pideClaveMaestra;

    public Vault(IRepositorioDelVault repositorio) => _repositorio = repositorio;

    /// <summary>Se dispara al bloquear, para que quien tenga un secreto descifrado vivo lo pise. Sin esto, bloquear no bloquea nada.</summary>
    public event Action? Bloqueado;

    /// <summary>Sin clave maestra siempre está abierto: no hay nada que abrir.</summary>
    public bool EstaAbierto => !_pideClaveMaestra || _claveDerivada.Length > 0;

    public bool PideClaveMaestra => _pideClaveMaestra;

    public async Task<ComoAbre> ComoAbreAsync(CancellationToken ct = default)
    {
        var fila = await _repositorio.LeerAsync(ct).ConfigureAwait(false);
        _pideClaveMaestra = fila is not null;

        return _pideClaveMaestra ? ComoAbre.ConLaClaveMaestra : ComoAbre.SinClaveMaestra;
    }

    public async Task<ResultadoDeApertura> AbrirConLaClaveMaestraAsync(
        ReadOnlyMemory<char> claveMaestra, CancellationToken ct = default)
    {
        await _candado.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (await LeerFilaSanaAsync(ct).ConfigureAwait(false) is not { } fila)
            {
                return ResultadoDeApertura.NoTieneClaveMaestra;
            }

            var derivada = Derivar(claveMaestra.Span, fila);

            switch (Verificar(fila, derivada))
            {
                case ResultadoDeApertura.Abierto:
                    Adoptar(derivada);
                    return ResultadoDeApertura.Abierto;

                default:
                    CryptographicOperations.ZeroMemory(derivada);
                    return ResultadoDeApertura.ClaveIncorrecta;
            }
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>Saca la clave derivada de memoria y avisa para que se pisen los secretos ya descifrados.</summary>
    public void Bloquear()
    {
        CryptographicOperations.ZeroMemory(_claveDerivada);
        _claveDerivada = [];
        Bloqueado?.Invoke();
    }

    /// <summary>
    /// Pone, cambia o quita la clave maestra. <paramref name="claveNueva"/> vacía la quita y deja
    /// los secretos en claro. <paramref name="claveActual"/> se exige cuando ya hay una.
    /// </summary>
    public async Task<bool> DefinirClaveMaestraAsync(
        ReadOnlyMemory<char> claveActual,
        ReadOnlyMemory<char> claveNueva,
        CancellationToken ct = default)
    {
        ExigirFormaValida(claveNueva);

        // Se relee en lugar de confiar en el campo: entre el arranque y este momento la fila pudo
        // cambiar, y de si existe depende que haya que exigir la clave actual.
        _pideClaveMaestra = await _repositorio.LeerAsync(ct).ConfigureAwait(false) is not null;

        if (_pideClaveMaestra
            && await AbrirConLaClaveMaestraAsync(claveActual, ct).ConfigureAwait(false)
               != ResultadoDeApertura.Abierto)
        {
            return false;
        }

        await _candado.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            ExigirAbierto();
            await RecifrarTodoAsync(claveNueva, ct).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _candado.Release();
        }
    }

    public async Task GuardarSecretoAsync(
        ReferenciaDeSecreto referencia, ReadOnlyMemory<char> secreto, CancellationToken ct = default)
    {
        ExigirAbierto();

        await _repositorio.GuardarSecretoAsync(referencia, Empaquetar(secreto.Span), ct)
            .ConfigureAwait(false);
    }

    /// <summary>Devuelve el secreto en un <c>char[]</c> para que el llamador lo pueda pisar. <c>null</c> cuando no hay ninguno guardado, que no es un error.</summary>
    public async Task<char[]?> LeerSecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default)
    {
        ExigirAbierto();

        if (await _repositorio.LeerSecretoAsync(referencia, ct).ConfigureAwait(false)
            is not { } guardado)
        {
            return null;
        }

        return Desempaquetar(guardado, referencia);
    }

    public Task BorrarSecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        _repositorio.BorrarSecretoAsync(referencia, ct);

    public Task<bool> HaySecretoAsync(
        ReferenciaDeSecreto referencia, CancellationToken ct = default) =>
        _repositorio.HaySecretoAsync(referencia, ct);

    public Task<IReadOnlyList<ReferenciaDeSecreto>> ReferenciasConSecretoAsync(
        CancellationToken ct = default) =>
        _repositorio.ReferenciasConSecretoAsync(ct);

    // Descifra todo con la clave que rige ahora y lo vuelve a cifrar con la que se pide, y escribe
    // la fila de la clave maestra y todos los secretos de una sola vez: si esto se corta por la
    // mitad, la base queda como estaba.
    private async Task RecifrarTodoAsync(ReadOnlyMemory<char> claveNueva, CancellationToken ct)
    {
        var guardados = await _repositorio.TodosLosSecretosAsync(ct).ConfigureAwait(false);
        var conClaveMaestra = !claveNueva.IsEmpty;

        FilaDelVault? fila = null;
        var derivada = Array.Empty<byte>();

        if (conClaveMaestra)
        {
            var sal = CifradoDeSecretos.SalNueva();
            var iteraciones = CifradoDeSecretos.IteracionesPorOmision;
            var hash = CifradoDeSecretos.HashPorOmision;

            derivada = CifradoDeSecretos.Derivar(claveNueva.Span, sal, iteraciones, hash);

            var verificador = CifradoDeSecretos.CifrarTexto(
                derivada, FilaDelVault.TextoDelVerificador);

            fila = new FilaDelVault(
                FilaDelVault.FormatoActual,
                hash.Name!,
                sal,
                iteraciones,
                verificador.Nonce,
                verificador.Cifrado);
        }

        try
        {
            var recifrados = new List<(ReferenciaDeSecreto, SecretoGuardado)>(guardados.Count);

            foreach (var (referencia, guardado) in guardados)
            {
                var claro = Desempaquetar(guardado, referencia);

                try
                {
                    recifrados.Add((referencia, conClaveMaestra
                        ? SecretoGuardado.Cifrado(CifradoDeSecretos.CifrarTexto(derivada, claro))
                        : EnClaro(claro)));
                }
                finally
                {
                    Array.Clear(claro);
                }
            }

            await _repositorio.ReemplazarTodoAsync(fila, recifrados, ct).ConfigureAwait(false);

            Adoptar(conClaveMaestra ? derivada : []);
            _pideClaveMaestra = conClaveMaestra;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(derivada);
            throw;
        }
    }

    private SecretoGuardado Empaquetar(ReadOnlySpan<char> secreto) =>
        _pideClaveMaestra
            ? SecretoGuardado.Cifrado(CifradoDeSecretos.CifrarTexto(_claveDerivada, secreto))
            : EnClaro(secreto);

    private char[] Desempaquetar(SecretoGuardado guardado, ReferenciaDeSecreto referencia)
    {
        if (!guardado.EstaCifrado)
        {
            return Encoding.UTF8.GetString(guardado.Bytes).ToCharArray();
        }

        try
        {
            return CifradoDeSecretos.DescifrarTexto(_claveDerivada, guardado.Sobre);
        }
        catch (CryptographicException)
        {
            // Sin este envase al llamador le llega una AuthenticationTagMismatchException que no
            // dice cual es la credencial ni que hay que hacer.
            throw new CredencialIlegibleException(referencia);
        }
    }

    private static SecretoGuardado EnClaro(ReadOnlySpan<char> secreto)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(secreto)];
        Encoding.UTF8.GetBytes(secreto, bytes);
        return new SecretoGuardado(bytes, null);
    }

    private static byte[] Derivar(ReadOnlySpan<char> claveMaestra, FilaDelVault fila) =>
        CifradoDeSecretos.Derivar(
            claveMaestra, fila.KdfSal, fila.KdfIteraciones, fila.Hash);

    /// <summary>El verificador es lo que separa «la clave está mal» de «la fila está dañada»: si no autentica, la clave era otra.</summary>
    private static ResultadoDeApertura Verificar(FilaDelVault fila, byte[] derivada)
    {
        try
        {
            var texto = CifradoDeSecretos.DescifrarTexto(derivada, fila.SobreDelVerificador);

            try
            {
                return texto.AsSpan().SequenceEqual(FilaDelVault.TextoDelVerificador)
                    ? ResultadoDeApertura.Abierto
                    : ResultadoDeApertura.ClaveIncorrecta;
            }
            finally
            {
                Array.Clear(texto);
            }
        }
        catch (CryptographicException)
        {
            return ResultadoDeApertura.ClaveIncorrecta;
        }
    }

    private async Task<FilaDelVault?> LeerFilaSanaAsync(CancellationToken ct)
    {
        var fila = await _repositorio.LeerAsync(ct).ConfigureAwait(false);

        if (fila?.PorQueNoSeUsa(FilaDelVault.FormatoActual, CifradoDeSecretos.IteracionesMinimas)
            is { } motivo)
        {
            throw new VaultDanadoException(motivo);
        }

        _pideClaveMaestra = fila is not null;

        return fila;
    }

    private static void ExigirFormaValida(ReadOnlyMemory<char> claveMaestra)
    {
        if (!claveMaestra.IsEmpty && !PoliticaDeClaveMaestra.Cumple(claveMaestra.Span))
        {
            throw new ArgumentException(
                PoliticaDeClaveMaestra.Explicar(PoliticaDeClaveMaestra.Revisar(claveMaestra.Span)),
                nameof(claveMaestra));
        }
    }

    private void Adoptar(byte[] clave)
    {
        CryptographicOperations.ZeroMemory(_claveDerivada);
        _claveDerivada = clave;
    }

    private void ExigirAbierto()
    {
        if (!EstaAbierto)
        {
            throw new VaultCerradoException();
        }
    }

    public void Dispose()
    {
        Bloquear();
        _candado.Dispose();
    }
}
