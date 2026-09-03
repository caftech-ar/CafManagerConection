using CafManagerConection.Domain.Connections;
using CafManagerConection.Domain.Credentials;

namespace CafManagerConection.Domain.Tests.Credentials;

// Verifica el Principio II: ningún secreto puede escaparse por un registro accidental ni
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

public class CredentialKeyTests
{
    [Theory]
    [InlineData(Protocol.Rdp, "rdp")]
    [InlineData(Protocol.Ssh, "ssh")]
    [InlineData(Protocol.Web, "web")]
    public void ForConnection_usa_el_formato_acordado(Protocol protocol, string scope)
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var key = CredentialKey.ForConnection(id, protocol);

        Assert.Equal($"cmc:{scope}:11111111-2222-3333-4444-555555555555", key.Value);
    }

    [Fact]
    public void ForFolder_incluye_el_protocolo_al_final()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var key = CredentialKey.ForFolder(id, Protocol.Ssh);

        Assert.Equal("cmc:folder:11111111-2222-3333-4444-555555555555:ssh", key.Value);
    }

    [Fact]
    public void Una_carpeta_tiene_una_clave_distinta_por_protocolo()
    {
        var id = Guid.NewGuid();

        var rdp = CredentialKey.ForFolder(id, Protocol.Rdp);
        var ssh = CredentialKey.ForFolder(id, Protocol.Ssh);

        Assert.NotEqual(rdp.Value, ssh.Value);
    }

    [Fact]
    public void La_clave_se_basa_en_el_identificador_no_en_el_nombre()
    {
        var id = Guid.NewGuid();

        var primera = CredentialKey.ForConnection(id, Protocol.Ssh);
        var segunda = CredentialKey.ForConnection(id, Protocol.Ssh);

        Assert.Equal(primera, segunda);
    }

    [Theory]
    [InlineData("otra:cosa:123")]
    [InlineData("")]
    public void FromStored_rechaza_una_clave_con_otro_prefijo(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => CredentialKey.FromStored(value));
    }

    [Fact]
    public void TryParse_reconoce_una_clave_valida()
    {
        Assert.True(CredentialKey.TryParse("cmc:ssh:abc", out var key));
        Assert.Equal("cmc:ssh:abc", key.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mal:formato")]
    public void TryParse_rechaza_lo_que_no_es_una_clave(string? value)
    {
        Assert.False(CredentialKey.TryParse(value, out _));
    }
}
