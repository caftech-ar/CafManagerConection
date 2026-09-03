using CafManagerConection.Domain.Credentials;
using CafManagerConection.Infrastructure.Credentials;

namespace CafManagerConection.Infrastructure.Tests.Credentials;

[Trait("Category", "WindowsCredentials")]
public class WindowsCredentialStoreTests : IDisposable
{
    private readonly WindowsCredentialStore _store = new();
    private readonly List<string> _created = [];

    private string NewKey()
    {
        var key = $"cmc:test:{Guid.NewGuid():N}";
        _created.Add(key);
        return key;
    }



    [Fact]
    public async Task Escribir_leer_y_borrar_completa_el_ciclo()
    {
        var key = NewKey();
        using (var cred = new StoredCredential("admin", null, "SecretaDePrueba123"))
        {
            await _store.WriteAsync(key, cred);
        }

        using var leida = await _store.ReadAsync(key);

        Assert.NotNull(leida);
        Assert.Equal("admin", leida.UserName);
        Assert.Equal("SecretaDePrueba123", leida.RevealSecret());

        await _store.DeleteAsync(key);
        Assert.Null(await _store.ReadAsync(key));
    }

    [Fact]
    public async Task Leer_una_clave_inexistente_devuelve_nulo_y_no_lanza()
    {
        // FR-039.

        var resultado = await _store.ReadAsync($"cmc:test:no-existe-{Guid.NewGuid():N}");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Borrar_una_clave_inexistente_es_exitoso()
    {

        var ex = await Record.ExceptionAsync(
            () => _store.DeleteAsync($"cmc:test:no-existe-{Guid.NewGuid():N}"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task El_dominio_viaja_junto_al_usuario()
    {
        var key = NewKey();
        using (var cred = new StoredCredential("admin", "CORP", "clave"))
        {
            await _store.WriteAsync(key, cred);
        }

        using var leida = await _store.ReadAsync(key);

        Assert.Equal("CORP", leida!.Domain);
        Assert.Equal("admin", leida.UserName);
    }

    [Fact]
    public async Task Exists_distingue_una_credencial_guardada_de_una_ausente()
    {
        var key = NewKey();
        using (var cred = new StoredCredential("u", null, "s"))
        {
            await _store.WriteAsync(key, cred);
        }

        Assert.True(await _store.ExistsAsync(key));
        await _store.DeleteAsync(key);
        Assert.False(await _store.ExistsAsync(key));
    }

    [Fact]
    public async Task Un_secreto_con_acentos_y_simbolos_sobrevive_el_viaje()
    {
        var key = NewKey();
        const string secreto = "Contraseña#2026·áéíóú·ñÑ·€";
        using (var cred = new StoredCredential("u", null, secreto))
        {
            await _store.WriteAsync(key, cred);
        }

        using var leida = await _store.ReadAsync(key);

        Assert.Equal(secreto, leida!.RevealSecret());
    }

    [Fact]
    public async Task Un_secreto_que_supera_el_limite_de_Windows_se_rechaza_con_un_mensaje_claro()
    {
        // El limite de 2560 bytes es de la plataforma y es la razon por la que las claves
        // privadas se referencian por ruta.
        var enorme = new string('x', 2000); // 4000 bytes en UTF-16
        using var cred = new StoredCredential("u", null, enorme);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _store.WriteAsync(NewKey(), cred));

        Assert.Contains("2560", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sobrescribir_una_credencial_reemplaza_el_secreto()
    {
        var key = NewKey();
        using (var vieja = new StoredCredential("u", null, "vieja"))
        {
            await _store.WriteAsync(key, vieja);
        }

        using (var nueva = new StoredCredential("u", null, "nueva"))
        {
            await _store.WriteAsync(key, nueva);
        }

        using var leida = await _store.ReadAsync(key);
        Assert.Equal("nueva", leida!.RevealSecret());
    }

    public void Dispose()
    {

        foreach (var key in _created)
        {
            try
            {
                _store.DeleteAsync(key).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }
        }
    }
}
