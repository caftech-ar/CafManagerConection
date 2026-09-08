using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;
using CafManagerConection.Domain.Importacion;
using CafManagerConection.UseCases.Abstractions;
using CafManagerConection.UseCases.Connections;
using CafManagerConection.UseCases.Credentials;
using CafManagerConection.UseCases.Importacion;
using NSubstitute;

namespace CafManagerConection.UseCases.Tests.Importacion;

// Con el vault cerrado, `CreateAsync` escribia la credencial ANTES del try que crea la
// conexion y el importador no atrapaba por conexion, asi que la primera sesion con contrasena
// abortaba el lote entero: no entraba ninguna conexion, ni las que no traian contrasena.
public sealed class ImportarConElVaultCerradoTests
{
    private readonly IConnectionRepository _conexiones = Substitute.For<IConnectionRepository>();
    private readonly IFolderRepository _carpetas = Substitute.For<IFolderRepository>();
    private readonly ICredentialStore _credenciales = Substitute.For<ICredentialStore>();

    public ImportarConElVaultCerradoTests()
    {
        _carpetas.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Folder>());
        _conexiones.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Connection>());
    }

    private void ConElVaultCerrado() =>
        _credenciales
            .WriteAsync(
                Arg.Any<ReferenciaDeSecreto>(),
                Arg.Any<ReadOnlyMemory<char>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new VaultCerradoException());

    private ImportadorDeConexiones Importador() =>
        new(_carpetas, _conexiones, new ConnectionService(_conexiones, _carpetas, _credenciales));

    private static ConexionImportada Sesion(string nombre, string? secreto) =>
        new(
            OrigenDeImportacion.Putty,
            nombre,
            [],
            "192.0.2.10",
            22,
            "admin",
            null,
            "ssh",
            secreto is null ? null : new StoredCredential("admin", null, secreto));

    [Fact]
    public async Task Con_el_vault_cerrado_entran_todas_las_conexiones()
    {
        ConElVaultCerrado();

        var resultado = await Importador().ImportarAsync(
            [Sesion("Con clave", "secreto-1"), Sesion("Sin clave", null), Sesion("Otra con clave", "secreto-2")],
            traerContrasenas: true);

        Assert.Equal(3, resultado.Creadas);
        Assert.Empty(resultado.Fallidas);
    }

    [Fact]
    public async Task Con_el_vault_cerrado_se_dice_cuantas_contrasenas_quedaron_afuera_y_por_que()
    {
        ConElVaultCerrado();

        var resultado = await Importador().ImportarAsync(
            [Sesion("Con clave", "secreto-1"), Sesion("Sin clave", null)],
            traerContrasenas: true);

        Assert.Equal(1, resultado.ContrasenasPerdidas);
        Assert.Equal(0, resultado.ContrasenasGuardadas);
        Assert.Contains("vault", resultado.ContrasenasSinTraer[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Con clave", resultado.ContrasenasSinTraer[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_vault_abierto_las_contrasenas_se_guardan_y_no_falta_ninguna()
    {
        var resultado = await Importador().ImportarAsync(
            [Sesion("Con clave", "secreto-1"), Sesion("Sin clave", null)],
            traerContrasenas: true);

        Assert.Equal(2, resultado.Creadas);
        Assert.Equal(1, resultado.ContrasenasGuardadas);
        Assert.Equal(0, resultado.ContrasenasPerdidas);
    }
}
