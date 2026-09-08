namespace CafManagerConection.Domain.Tests;

// Las dos reglas de arquitectura nuevas leen el arbol de trabajo, asi que sin estas pruebas no se
// sabe si detectan el defecto o si simplemente no encontraron nada.
public sealed class ArquitecturaDetectaLosDefectosTests
{
    [Fact]
    public void Se_leen_las_referencias_de_proyecto_de_un_csproj()
    {
        var csproj = Path.Combine(Path.GetTempPath(), $"prueba-{Guid.NewGuid():N}.csproj");

        File.WriteAllText(csproj, """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\CafManagerConection.UseCases\CafManagerConection.UseCases.csproj" />
                <ProjectReference Include="..\CafManagerConection.Platform\CafManagerConection.Platform.csproj" />
              </ItemGroup>
            </Project>
            """);

        try
        {
            var referencias = ArchitectureTests.ReferenciasDe(csproj).ToArray();

            Assert.Equal(
                ["CafManagerConection.UseCases", "CafManagerConection.Platform"], referencias);
        }
        finally
        {
            File.Delete(csproj);
        }
    }

    [Fact]
    public void Un_csproj_sin_referencias_no_devuelve_ninguna()
    {
        var csproj = Path.Combine(Path.GetTempPath(), $"prueba-{Guid.NewGuid():N}.csproj");
        File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            Assert.Empty(ArchitectureTests.ReferenciasDe(csproj));
        }
        finally
        {
            File.Delete(csproj);
        }
    }

    [Fact]
    public void Una_interfaz_con_la_firma_de_ejecucion_remota_se_detecta() =>
        Assert.True(ArchitectureTests.DeclaraEjecucionRemota([
            "public interface IOtroEjecutor",
            "{",
            "    Task<bool> RunAsync(string command, int timeoutSeconds, CancellationToken ct);",
            "}",
        ]));

    [Fact]
    public void Una_clase_con_la_misma_firma_no_es_una_declaracion_de_puerto() =>
        Assert.False(ArchitectureTests.DeclaraEjecucionRemota([
            "public sealed class Ejecutor",
            "{",
            "    public Task<bool> RunAsync(string command, int timeoutSeconds) => null!;",
            "}",
        ]));

    [Fact]
    public void Una_interfaz_de_otra_cosa_no_se_marca() =>
        Assert.False(ArchitectureTests.DeclaraEjecucionRemota([
            "public interface IRepositorio",
            "{",
            "    Task GuardarAsync(string clave, CancellationToken ct);",
            "}",
        ]));

    // El caso que importa: la firma aparece despues de que la interfaz cerro, en otro tipo.
    [Fact]
    public void La_firma_fuera_de_la_interfaz_no_cuenta() =>
        Assert.False(ArchitectureTests.DeclaraEjecucionRemota([
            "public interface IVacia",
            "{",
            "}",
            "",
            "public sealed class Ejecutor",
            "{",
            "    public Task<bool> RunAsync(string command, int timeoutSeconds) => null!;",
            "}",
        ]));
}
