using CafManagerConection.Domain.Ssh;

namespace CafManagerConection.Domain.Tests.Ssh;

// La huella tiene que coincidir carácter por carácter con la que ya conoce OpenSSH.
public sealed class HuellaSshTests
{
    // Blob público de "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIE4mPoqozJT5vQeWSDT8mBohbg5W2QV9nacc
    // YsRwmgec", una clave sintética generada con ssh-keygen para esta prueba. La huella se
    // verificó antes con "ssh-keygen -lf" sobre la clave privada correspondiente.
    private const string BlobPublicoBase64 =
        "AAAAC3NzaC1lZDI1NTE5AAAAIE4mPoqozJT5vQeWSDT8mBohbg5W2QV9naccYsRwmgec";

    private const string HuellaEsperada = "SHA256:Ceb3Td4YTqUXha3IiKhELCcDGrsgZpmx7F9vCvRmB/s";

    [Fact]
    public void Coincide_con_la_huella_que_calcula_ssh_keygen_lf()
    {
        var blob = Convert.FromBase64String(BlobPublicoBase64);

        var huella = HuellaSsh.CalcularSha256(blob);

        Assert.Equal(HuellaEsperada, huella);
    }

    [Fact]
    public void No_lleva_relleno_de_base64()
    {
        var blob = Convert.FromBase64String(BlobPublicoBase64);

        var huella = HuellaSsh.CalcularSha256(blob);

        Assert.DoesNotContain('=', huella);
    }

    [Fact]
    public void Empieza_siempre_con_el_prefijo_SHA256()
    {
        var huella = HuellaSsh.CalcularSha256([1, 2, 3]);

        Assert.StartsWith("SHA256:", huella, StringComparison.Ordinal);
    }

    [Fact]
    public void Blobs_distintos_dan_huellas_distintas()
    {
        var huellaA = HuellaSsh.CalcularSha256([1, 2, 3]);
        var huellaB = HuellaSsh.CalcularSha256([1, 2, 4]);

        Assert.NotEqual(huellaA, huellaB);
    }
}
