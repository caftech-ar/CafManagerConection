using System.Net.NetworkInformation;

namespace CafManagerConection.App.Services;

/// <summary>Elige el puerto local con el que proponer un túnel nuevo (FR-168a y FR-168b).</summary>
public static class PuertoLocalSugerido
{
    // Por debajo de la franja efímera de Windows, que arranca en 49152.
    /// <summary>Primer puerto de la franja alta donde buscar cuando el deseado está tomado.</summary>
    private const int PrimeroDeLaFranjaAlta = 10000;

    private const int UltimoDeLaFranjaAlta = 49151;

    /// <summary>Propone un puerto local libre para llegar al puerto remoto.</summary>
    public static int Elegir(int puertoRemoto, IReadOnlySet<int> tomados)
    {
        if (puertoRemoto is >= 1 and <= 65535 && !tomados.Contains(puertoRemoto))
        {
            return puertoRemoto;
        }

        var arranque = puertoRemoto < PrimeroDeLaFranjaAlta
            ? PrimeroDeLaFranjaAlta + puertoRemoto
            : PrimeroDeLaFranjaAlta;

        for (var puerto = arranque; puerto <= UltimoDeLaFranjaAlta; puerto++)
        {
            if (!tomados.Contains(puerto))
            {
                return puerto;
            }
        }

        for (var puerto = PrimeroDeLaFranjaAlta; puerto < arranque; puerto++)
        {
            if (!tomados.Contains(puerto))
            {
                return puerto;
            }
        }

        return puertoRemoto;
    }

    /// <summary>Puertos que este equipo ya tiene a la escucha, más los que otros túneles reservaron.</summary>
    public static IReadOnlySet<int> Tomados(IEnumerable<int> reservadosPorTuneles)
    {
        var tomados = new HashSet<int>(reservadosPorTuneles);

        try
        {
            foreach (var punto in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
            {
                tomados.Add(punto.Port);
            }
        }
        catch (NetworkInformationException)
        {
        }

        return tomados;
    }
}
