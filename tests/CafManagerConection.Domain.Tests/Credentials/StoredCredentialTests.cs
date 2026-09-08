using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.Domain.Tests.Credentials;

// Ningún secreto puede escaparse por un registro accidental ni
// quedar vivo en memoria más de lo necesario.
public class StoredCredentialTests
{
    [Fact]
    public void ToString_nunca_revela_el_secreto()
    {
        using var cred = new StoredCredential("admin", null, "SuperSecreta123");

        var texto = cred.ToString();

        Assert.DoesNotContain("SuperSecreta123", texto, StringComparison.Ordinal);
        Assert.Equal("StoredCredential(redactada)", texto);
    }

    [Fact]
    public void Interpolar_la_credencial_no_revela_el_secreto()
    {
        using var cred = new StoredCredential("admin", null, "SuperSecreta123");

        var linea = $"Conectando con {cred}";

        Assert.DoesNotContain("SuperSecreta123", linea, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_limpia_el_secreto_de_memoria()
    {
        var cred = new StoredCredential("admin", null, "SuperSecreta123");

        cred.Dispose();

        Assert.False(cred.HasSecret);
        Assert.Throws<ObjectDisposedException>(() => cred.RevealSecret());
    }

    [Fact]
    public void Dispose_es_idempotente()
    {
        var cred = new StoredCredential("admin", null, "x");

        cred.Dispose();
        cred.Dispose();

        Assert.False(cred.HasSecret);
    }

    [Fact]
    public void RevealSecret_devuelve_el_secreto_mientras_este_viva()
    {
        using var cred = new StoredCredential("admin", "CORP", "clave");

        Assert.Equal("clave", cred.RevealSecret());
        Assert.Equal("admin", cred.UserName);
        Assert.Equal("CORP", cred.Domain);
    }

    [Fact]
    public void Una_credencial_sin_secreto_lo_informa()
    {
        using var cred = new StoredCredential("admin", null, ReadOnlySpan<char>.Empty);

        Assert.False(cred.HasSecret);
    }

    [Fact]
    public void El_tipo_no_es_serializable()
    {
        var atributos = typeof(StoredCredential).GetCustomAttributes(inherit: false);

        Assert.DoesNotContain(atributos, a => a.GetType().Name.Contains("Serializable"));
    }
}
