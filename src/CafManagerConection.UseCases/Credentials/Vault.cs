using System.Security.Cryptography;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.UseCases.Abstractions;

namespace CafManagerConection.UseCases.Credentials;

public enum ComoAbre
{
    /// <summary>Todavía no existe: hay que crearlo.</summary>
    SinCrear,

    /// <summary>Se abre sin preguntar nada, porque este equipo está recordado.</summary>
    Solo,

    /// <summary>Hace falta la clave maestra.</summary>
    ConLaClaveMaestra,
}

public sealed class VaultCerradoException : Exception
{
    public VaultCerradoException()
        : base("El vault está cerrado: hace falta la clave maestra para leer o guardar una credencial.")
    {
    }
}

/// <summary>El vault de credenciales. La clave maestra es OPCIONAL: sin ella, la clave del vault queda atada a este usuario de Windows y el vault abre solo.</summary>
public sealed class Vault : IDisposable
{
    private readonly IRepositorioDelVault _repositorio;
    private readonly IProteccionDeEquipo _equipo;

    private byte[] _claveDelVault = [];
    private bool _bloqueadoAMano;

    public Vault(IRepositorioDelVault repositorio, IProteccionDeEquipo equipo)
    {
        _repositorio = repositorio;
        _equipo = equipo;
    }

    public bool EstaAbierto => _claveDelVault.Length > 0;

    public async Task<ComoAbre> ComoAbreAsync(CancellationToken ct = default)
    {
        var fila = await _repositorio.LeerAsync(ct).ConfigureAwait(false);

        if (fila is null)
        {
            return ComoAbre.SinCrear;
        }

        // Bloquear a mano desarma el desbloqueo automatico: si no, bloquear no bloquea nada.
        return fila.AbreSola && !_bloqueadoAMano ? ComoAbre.Solo : ComoAbre.ConLaClaveMaestra;
    }

    /// <summary>Crea el vault. <paramref name="claveMaestra"/> vacía significa sin clave maestra, y entonces <paramref name="recordarEsteEquipo"/> tiene que ser <c>true</c> o el vault no se podría abrir.</summary>
    public async Task CrearAsync(
        ReadOnlyMemory<char> claveMaestra,
        bool recordarEsteEquipo,
        CancellationToken ct = default)
    {
        if (claveMaestra.IsEmpty && !recordarEsteEquipo)
        {
            throw new ArgumentException(
                "Sin clave maestra hay que recordar este equipo, o el vault no se abriría nunca.",
                nameof(recordarEsteEquipo));
        }

        if (!claveMaestra.IsEmpty && !PoliticaDeClaveMaestra.Cumple(claveMaestra.Span))
        {
            throw new ArgumentException(
                PoliticaDeClaveMaestra.Explicar(PoliticaDeClaveMaestra.Revisar(claveMaestra.Span)),
                nameof(claveMaestra));
        }

        var clave = CifradoDeSecretos.ClaveNueva();

        await GuardarLasEnvolturasAsync(clave, claveMaestra, recordarEsteEquipo, ct)
            .ConfigureAwait(false);

        Adoptar(clave);
    }

    public async Task<bool> AbrirSoloAsync(CancellationToken ct = default)
    {
        var fila = await _repositorio.LeerAsync(ct).ConfigureAwait(false);

        if (fila?.ClaveDpapi is not { Length: > 0 } envuelta || _bloqueadoAMano)
        {
            return false;
        }

        try
        {
            Adoptar(_equipo.Desproteger(envuelta));
            return true;
        }
        catch (CryptographicException)
        {
            // Otro usuario de Windows, otra maquina o un blob tocado. Es el camino normal.
            return false;
        }
    }

    public async Task<bool> AbrirConLaClaveMaestraAsync(
        ReadOnlyMemory<char> claveMaestra, CancellationToken ct = default)
    {
        var fila = await _repositorio.LeerAsync(ct).ConfigureAwait(false);

        if (fila?.SobreDeLaClaveMaestra is not { } sobre || fila.KdfSal is null
            || fila.KdfIteraciones is not { } iteraciones)
        {
            return false;
        }

        var derivada = CifradoDeSecretos.Derivar(claveMaestra.Span, fila.KdfSal, iteraciones);

        try
        {
            Adoptar(CifradoDeSecretos.Descifrar(derivada, sobre));
            _bloqueadoAMano = false;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivada);
        }
    }

    /// <summary>Saca las claves de memoria y desarma el desbloqueo automático hasta que se vuelva a tipear la clave maestra.</summary>
    public void Bloquear()
    {
        CryptographicOperations.ZeroMemory(_claveDelVault);
        _claveDelVault = [];
        _bloqueadoAMano = true;
    }

    /// <summary>Pone, cambia o quita la clave maestra. Vacía la quita, y entonces el equipo tiene que quedar recordado.</summary>
    public async Task DefinirClaveMaestraAsync(
        ReadOnlyMemory<char> claveMaestra,
        bool recordarEsteEquipo,
        CancellationToken ct = default)
    {
        ExigirAbierto();

        if (claveMaestra.IsEmpty && !recordarEsteEquipo)
        {
            throw new ArgumentException(
                "Quitar la clave maestra sin recordar este equipo dejaría el vault sin forma de abrirse.",
                nameof(recordarEsteEquipo));
        }

        if (!claveMaestra.IsEmpty && !PoliticaDeClaveMaestra.Cumple(claveMaestra.Span))
        {
            throw new ArgumentException(
                PoliticaDeClaveMaestra.Explicar(PoliticaDeClaveMaestra.Revisar(claveMaestra.Span)),
                nameof(claveMaestra));
        }

        // Solo se reenvuelve la clave del vault: las credenciales no se recifran, asi que un
        // corte a mitad de camino no puede dejar la mitad ilegible.
        await GuardarLasEnvolturasAsync(_claveDelVault, claveMaestra, recordarEsteEquipo, ct)
            .ConfigureAwait(false);
    }

    public async Task OlvidarEsteEquipoAsync(CancellationToken ct = default)
    {
        var fila = await _repositorio.LeerAsync(ct).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("El vault no existe.");

        if (!fila.PideClaveMaestra)
        {
            throw new InvalidOperationException(
                "Sin clave maestra configurada, olvidar este equipo dejaría el vault sin forma de "
                + "abrirse. Poné una clave maestra primero.");
        }

        await _repositorio.GuardarAsync(fila with { ClaveDpapi = null }, ct).ConfigureAwait(false);
    }

    public async Task GuardarCredencialAsync(
        string clave,
        string usuario,
        string? dominio,
        ReadOnlyMemory<char> secreto,
        CancellationToken ct = default)
    {
        ExigirAbierto();

        var sobre = CifradoDeSecretos.CifrarTexto(_claveDelVault, secreto.Span);

        await _repositorio.GuardarCredencialAsync(
            new CredencialCifrada(clave, usuario, dominio, sobre), ct).ConfigureAwait(false);
    }

    /// <summary>Devuelve el secreto en un <c>char[]</c> para que el llamador lo pueda pisar. <c>null</c> cuando no existe, que no es un error.</summary>
    public async Task<(string Usuario, string? Dominio, char[] Secreto)?> LeerCredencialAsync(
        string clave, CancellationToken ct = default)
    {
        ExigirAbierto();

        var guardada = await _repositorio.LeerCredencialAsync(clave, ct).ConfigureAwait(false);

        return guardada is null
            ? null
            : (guardada.Usuario,
               guardada.Dominio,
               CifradoDeSecretos.DescifrarTexto(_claveDelVault, guardada.Sobre));
    }

    private async Task GuardarLasEnvolturasAsync(
        byte[] claveDelVault,
        ReadOnlyMemory<char> claveMaestra,
        bool recordarEsteEquipo,
        CancellationToken ct)
    {
        byte[]? dpapi = recordarEsteEquipo ? _equipo.Proteger(claveDelVault) : null;

        byte[]? sal = null;
        int? iteraciones = null;
        SobreCifrado? sobre = null;

        if (!claveMaestra.IsEmpty)
        {
            sal = CifradoDeSecretos.SalNueva();
            iteraciones = CifradoDeSecretos.IteracionesPorOmision;

            var derivada = CifradoDeSecretos.Derivar(claveMaestra.Span, sal, iteraciones.Value);

            try
            {
                sobre = CifradoDeSecretos.Cifrar(derivada, claveDelVault);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(derivada);
            }
        }

        await _repositorio.GuardarAsync(
            new FilaDelVault(
                FilaDelVault.FormatoActual,
                dpapi,
                sal,
                iteraciones,
                sobre?.Nonce,
                sobre?.Cifrado),
            ct).ConfigureAwait(false);
    }

    private void Adoptar(byte[] clave)
    {
        CryptographicOperations.ZeroMemory(_claveDelVault);
        _claveDelVault = clave;
    }

    private void ExigirAbierto()
    {
        if (!EstaAbierto)
        {
            throw new VaultCerradoException();
        }
    }

    public void Dispose() => Bloquear();
}
