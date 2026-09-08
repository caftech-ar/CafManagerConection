using CafManagerConection.Ssh;
using Xunit;

namespace CafManagerConection.Ssh.Tests;

public sealed class EscaladaASudoTests
{
    // Lo que contestó servidor-uno al correr `supervisorctl status` sin sudo: el fallo de
    // permiso en xmlrpc.py:560, escrito en la salida estándar en vez de en la de error.
    private const string ErrorDeSupervisorEnSalidaEstandar =
        "error: <class 'PermissionError'>, [Errno 13] Permission denied: file: "
        + "/usr/lib/python3/dist-packages/supervisor/xmlrpc.py line: 560";

    [Fact]
    public void Un_fallo_de_permiso_en_la_salida_estandar_se_reconoce()
    {
        var r = new CommandResult(1, ErrorDeSupervisorEnSalidaEstandar, string.Empty);

        Assert.False(r.Success);
        Assert.True(r.LooksLikePermissionDenied);
    }

    [Fact]
    public void Un_fallo_de_permiso_en_la_salida_de_error_sigue_reconociendose()
    {
        var r = new CommandResult(1, string.Empty, "cat: /etc/shadow: Permission denied");

        Assert.True(r.LooksLikePermissionDenied);
    }

    [Fact]
    public void El_socket_de_Docker_sigue_reconociendose()
    {
        var r = new CommandResult(
            1,
            string.Empty,
            "Got permission denied while trying to connect to the Docker daemon socket at "
            + "unix:///var/run/docker.sock");

        Assert.True(r.LooksLikePermissionDenied);
    }

    [Theory]
    [InlineData("mkdir: cannot create directory: Operation not permitted")]
    [InlineData("nginx: [emerg] must be root")]
    [InlineData("OSError: [Errno 13] denegado")]
    public void Otras_formas_de_decir_lo_mismo_tambien_cuentan(string error) =>
        Assert.True(new CommandResult(1, string.Empty, error).LooksLikePermissionDenied);

    [Fact]
    public void Un_fallo_que_no_es_de_permiso_no_escala()
    {
        var r = new CommandResult(127, string.Empty, "supervisorctl: command not found");

        Assert.False(r.LooksLikePermissionDenied);
        Assert.False(r.NeedsSudoPassword);
    }

    [Fact]
    public void Una_tabla_valida_con_estado_tres_no_parece_falta_de_permiso()
    {
        const string Tabla =
            "operador-inventario              RUNNING   pid 2316422, uptime 17:12:00\n"
            + "operador-inventario-to           FATAL     Exited too quickly (process log may "
            + "have details)\n";

        var r = new CommandResult(3, Tabla, string.Empty);

        Assert.False(r.Success);
        Assert.False(r.LooksLikePermissionDenied);
        Assert.False(r.NeedsSudoPassword);
    }

    [Theory]
    [InlineData("sudo: a password is required")]
    [InlineData("sudo: no tty present and no askpass program specified")]
    [InlineData("sudo: a terminal is required to read the password")]
    public void Sudo_pidiendo_contrasena_se_reconoce(string error) =>
        Assert.True(new CommandResult(1, string.Empty, error).NeedsSudoPassword);

    [Fact]
    public void Un_permiso_denegado_no_es_lo_mismo_que_sudo_pidiendo_contrasena()
    {
        var r = new CommandResult(1, ErrorDeSupervisorEnSalidaEstandar, string.Empty);

        Assert.True(r.LooksLikePermissionDenied);
        Assert.False(r.NeedsSudoPassword);
    }

    [Fact]
    public void Un_comando_que_anduvo_no_escala_por_mas_que_mencione_permisos()
    {
        var r = new CommandResult(0, "10:02 sshd: permission denied for user pepe", string.Empty);

        Assert.True(r.Success);

        Assert.True(r.LooksLikePermissionDenied);
    }
}
