using System.Runtime.Versioning;

namespace CafManagerConection.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class HerramientasDisponibles(BuscadorDeHerramientas buscador)
{
    private static readonly HerramientaExterna[] Todas =
        Enum.GetValues<HerramientaExterna>();

    private Dictionary<HerramientaExterna, string> _rutas = [];

    /// <summary>Cero mientras no se detectó, uno después. Es también el cerrojo que garantiza «una sola vez».</summary>
    private int _detectado;

    public bool Listo => Volatile.Read(ref _detectado) == 1;

    public async Task DetectarUnaVezAsync()
    {
        if (Interlocked.CompareExchange(ref _detectado, -1, 0) != 0)
        {
            return;
        }

        var encontradas = await Task.Run(() =>
        {
            var mapa = new Dictionary<HerramientaExterna, string>();

            foreach (var herramienta in Todas)
            {
                if (buscador.Buscar(herramienta) is { Length: > 0 } ruta)
                {
                    mapa[herramienta] = ruta;
                }
            }

            return mapa;
        }).ConfigureAwait(false);

        _rutas = encontradas;
        Volatile.Write(ref _detectado, 1);
    }

    public string? Ruta(HerramientaExterna herramienta) =>
        Listo && _rutas.TryGetValue(herramienta, out var ruta) ? ruta : null;

    public bool Hay(HerramientaExterna herramienta) => Ruta(herramienta) is not null;

    public IEnumerable<HerramientaExterna> Instaladas => Todas.Where(Hay);
}
