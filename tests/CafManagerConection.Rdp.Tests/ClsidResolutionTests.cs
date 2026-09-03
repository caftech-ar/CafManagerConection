using System.Runtime.Versioning;
using CafManagerConection.Rdp;

namespace CafManagerConection.Rdp.Tests;

// Defecto real: MsTscAx.MsTscAx.13 registra en Windows 11 con un CLSID que devuelve
// CLASS_E_CLASSNOTAVAILABLE al crearse; el código se quedaba con la primera versión que
// resolvía por ProgID sin probar la siguiente. No se instancia el control (un AxHost sin
// ventana/bomba de mensajes cuelga el proceso); ese ensayo vive en poc/.
[SupportedOSPlatform("windows")]
public sealed class ClsidResolutionTests
{
    [Fact]
    public void El_cliente_RDP_esta_disponible_en_este_equipo()
    {
        Assert.True(
            RdpClientHost.IsAvailable,
            "No se encontró ninguna versión activable del cliente RDP. En Windows 11 debería "
            + "haber al menos una; revisar que mstscax.dll esté registrado.");
    }

    [Fact]
    public void La_version_elegida_se_puede_activar()
    {
        if (!RdpClientHost.IsAvailable)
        {
            return;
        }

        var clsid = RdpClientHost.ResolvedClsid;

        Assert.NotEqual(string.Empty, clsid);

        Assert.True(
            RdpClientHost.CanCreate(Guid.Parse(clsid)),
            $"La versión elegida ({clsid}) está registrada pero no se puede activar. Es "
            + "exactamente el defecto que esta prueba cubre: hay que seguir buscando en las "
            + "versiones anteriores en lugar de quedarse con la primera que resuelve el ProgID.");
    }

    [Fact]
    public void Un_clsid_inexistente_no_se_puede_activar()
    {
        Assert.False(RdpClientHost.CanCreate(Guid.NewGuid()));
    }
}
