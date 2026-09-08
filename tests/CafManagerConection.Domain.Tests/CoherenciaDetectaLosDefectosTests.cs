namespace CafManagerConection.Domain.Tests;

// Las expresiones de CoherenciaDelCodigoTests leen el arbol de trabajo: sin estas pruebas no se
// sabe si detectan el defecto o si simplemente no encontraron nada.
public sealed class CoherenciaDetectaLosDefectosTests
{
    [Theory]
    [InlineData("mirá `DatabaseInitializer.cs:78` y listo")]
    [InlineData("está en Vault.cs:216")]
    [InlineData("lo dice `Estilos.xaml:1251`")]
    [InlineData("ver `scripts/credenciales.ps1:12`")]
    [InlineData("la constitución 2.1.0 lo exige")]
    [InlineData("según la constitución v1.1.0")]
    [InlineData("apunta a constitution.md:527")]
    public void Una_cita_por_numero_de_linea_o_de_version_se_detecta(string linea) =>
        Assert.True(
            CoherenciaDelCodigoTests.EsCitaPorLinea(linea),
            $"El guardián no vio la cita en «{linea}».");

    [Theory]
    [InlineData("citá `DatabaseInitializer.Migrate()`")]
    [InlineData("el Principio II · Cero secretos en claro")]
    [InlineData("está en `src/CafManagerConection.Domain/Credentials/SobreCifrado.cs`")]
    [InlineData("un marcador como `DatabaseInitializer.cs:NN` no es una cita")]
    public void Una_cita_bien_escrita_no_se_marca(string linea) =>
        Assert.False(
            CoherenciaDelCodigoTests.EsCitaPorLinea(linea),
            $"El guardián marcó como defecto algo correcto: «{linea}».");

    [Fact]
    public void Una_ruta_completa_se_extrae_y_una_abreviada_no()
    {
        Assert.Contains(
            "src/CafManagerConection.Domain/NoExiste.cs",
            CoherenciaDelCodigoTests.RutasCitadas(
                "falta `src/CafManagerConection.Domain/NoExiste.cs` acá"));

        Assert.Empty(CoherenciaDelCodigoTests.RutasCitadas("resumida `src/…/NoExiste.cs`"));
    }

    [Theory]
    [InlineData("esto lo pide FR-999")]
    [InlineData("el orden que fija SC-999a")]
    // Antes esta linea quedaba exenta porque definia el requisito en una spec. Sin specs, definir
    // uno es tambien citarlo: la regla ahora es que no exista ninguno.
    [InlineData("- **FR-999**: el sistema tiene que hacer algo.")]
    [InlineData("// FR-999.")]
    [InlineData("public sealed class SC052_ContrasenaDeSudoTests")]
    [InlineData("tests/CafManagerConection.Ssh.Tests/FR999_AlgoTests.cs")]
    public void Un_identificador_de_requisito_se_detecta(string linea) =>
        Assert.NotEmpty(CoherenciaDelCodigoTests.IdentificadoresCitados(linea));

    [Theory]
    [InlineData("el reintento con sudo llega a ejecutarse")]
    [InlineData("FR sin numero, o SC- suelto, no son identificadores")]
    [InlineData("la version 0.1.1 no es un requisito")]
    [InlineData("el codigo SC2 de un solo digito no es un identificador")]
    public void Lo_que_no_es_un_identificador_no_se_marca(string linea) =>
        Assert.Empty(CoherenciaDelCodigoTests.IdentificadoresCitados(linea));

    [Theory]
    [InlineData("nunca contiene el secreto (Principio II)")]
    [InlineData("lo exige el Principio III y algo mas")]
    [InlineData("Piso de la constitución. Una fila con menos se rechaza")]
    [InlineData("constitucion, Principio I · Dominio aislado")]
    public void Una_cita_a_la_constitucion_se_detecta(string linea) =>
        Assert.True(
            CoherenciaDelCodigoTests.CitaLaConstitucion(linea),
            $"El guardián no vio la cita en «{linea}».");

    [Theory]
    [InlineData("nunca contiene el secreto")]
    [InlineData("el principio de que nada se escribe en claro")]
    [InlineData("Principio, sin numero romano, es una palabra normal")]
    public void Enunciar_la_regla_sin_citar_no_se_marca(string linea) =>
        Assert.False(
            CoherenciaDelCodigoTests.CitaLaConstitucion(linea),
            $"El guardián marcó como defecto algo correcto: «{linea}».");
}
