namespace CafManagerConection.Domain.Tests;

// El 02/09/2026 el repositorio tenía 12.738 líneas de comentario contra 55.293 de código, 18%.
// Podarlo dejó 4.347 y ningún test cambió de resultado: nada de eso sostenía nada.
public sealed class DensidadDeComentarioTests
{
    private const double Techo = 0.05;

    public static TheoryData<string> Proyectos()
    {
        var datos = new TheoryData<string>();

        foreach (var carpeta in new[] { "src", "tests" })
        {
            foreach (var proyecto in Directory.EnumerateDirectories(
                         Path.Combine(Repositorio.Raiz(), carpeta)))
            {
                datos.Add(Path.Combine(carpeta, Path.GetFileName(proyecto)));
            }
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Proyectos))]
    public void Ningun_proyecto_pasa_el_cinco_por_ciento_de_comentario(string proyecto)
    {
        var porArchivo = ArchivosDe(proyecto)
            .Select(a => (Archivo: a, Cuenta: Contar(File.ReadAllText(a))))
            .ToList();

        var comentario = porArchivo.Sum(p => p.Cuenta.Comentario);
        var codigo = porArchivo.Sum(p => p.Cuenta.Codigo);

        if (codigo == 0 || comentario <= (codigo + comentario) * Techo)
        {
            return;
        }

        var peores = porArchivo
            .Where(p => p.Cuenta.Codigo > 0)
            .OrderByDescending(p => p.Cuenta.Comentario)
            .Take(10)
            .Select(p => $"  {Path.GetFileName(p.Archivo)}: "
                         + $"{p.Cuenta.Comentario} de {p.Cuenta.Comentario + p.Cuenta.Codigo}");

        Assert.Fail(
            $"{proyecto} tiene {comentario} líneas de comentario contra {codigo} de código, "
            + $"{(double)comentario / (comentario + codigo):P1}. El techo es {Techo:P0}."
            + Environment.NewLine
            + "El código se explica solo: si hace falta un comentario para entenderlo, lo que hay "
            + "que arreglar es el nombre, la responsabilidad o el largo del método."
            + Environment.NewLine
            + string.Join(Environment.NewLine, peores));
    }

    private static IEnumerable<string> ArchivosDe(string proyecto) =>
        Directory.EnumerateFiles(
                Path.Combine(Repositorio.Raiz(), proyecto), "*.cs", SearchOption.AllDirectories)
            .Where(a => !Generado(a));

    private static bool Generado(string ruta)
    {
        var separador = Path.DirectorySeparatorChar;

        return ruta.Contains($"{separador}bin{separador}", StringComparison.Ordinal)
               || ruta.Contains($"{separador}obj{separador}", StringComparison.Ordinal)
               || ruta.EndsWith(".g.cs", StringComparison.Ordinal)
               || ruta.EndsWith(".g.i.cs", StringComparison.Ordinal)
               || ruta.EndsWith(".Designer.cs", StringComparison.Ordinal);
    }

    internal readonly record struct Cuenta(int Comentario, int Codigo);

    /// <summary>Una línea con código y comentario cuenta como código: lo que se mide es la línea que sólo comenta.</summary>
    internal static Cuenta Contar(string fuente)
    {
        var comentario = 0;
        var codigo = 0;

        foreach (var linea in Clasificar(fuente))
        {
            if (linea.TieneCodigo)
            {
                codigo++;
            }
            else if (linea.TieneComentario)
            {
                comentario++;
            }
        }

        return new Cuenta(comentario, codigo);
    }

    private readonly record struct Linea(bool TieneCodigo, bool TieneComentario);

    private static List<Linea> Clasificar(string fuente)
    {
        var lineas = new List<Linea>();
        var codigo = false;
        var comentario = false;
        var estado = Estado.Normal;
        var comillasCrudas = 0;
        var i = 0;

        void Cerrar()
        {
            lineas.Add(new Linea(codigo, comentario));
            codigo = false;
            comentario = false;
        }

        while (i < fuente.Length)
        {
            var c = fuente[i];

            if (c == '\r')
            {
                i++;
                continue;
            }

            if (c == '\n')
            {
                if (estado == Estado.ComentarioDeLinea)
                {
                    estado = Estado.Normal;
                }

                Cerrar();
                i++;
                continue;
            }

            switch (estado)
            {
                case Estado.ComentarioDeLinea:
                    comentario = true;
                    i++;
                    continue;

                case Estado.ComentarioDeBloque:
                    comentario = true;

                    if (c == '*' && i + 1 < fuente.Length && fuente[i + 1] == '/')
                    {
                        estado = Estado.Normal;
                        i += 2;
                        continue;
                    }

                    i++;
                    continue;

                case Estado.Texto:
                    codigo = true;

                    if (c == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        estado = Estado.Normal;
                    }

                    i++;
                    continue;

                case Estado.TextoLiteral:
                    codigo = true;

                    if (c == '"')
                    {
                        if (i + 1 < fuente.Length && fuente[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }

                        estado = Estado.Normal;
                    }

                    i++;
                    continue;

                case Estado.TextoCrudo:
                    codigo = true;

                    if (c == '"')
                    {
                        var seguidas = Seguidas(fuente, i, '"');

                        if (seguidas >= comillasCrudas)
                        {
                            estado = Estado.Normal;
                            i += seguidas;
                            continue;
                        }

                        i += seguidas;
                        continue;
                    }

                    i++;
                    continue;

                case Estado.Caracter:
                    codigo = true;

                    if (c == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (c == '\'')
                    {
                        estado = Estado.Normal;
                    }

                    i++;
                    continue;
            }

            if (c == '/' && i + 1 < fuente.Length && fuente[i + 1] == '/')
            {
                estado = Estado.ComentarioDeLinea;
                comentario = true;
                i += 2;
                continue;
            }

            if (c == '/' && i + 1 < fuente.Length && fuente[i + 1] == '*')
            {
                estado = Estado.ComentarioDeBloque;
                comentario = true;
                i += 2;
                continue;
            }

            if (c == '"')
            {
                var seguidas = Seguidas(fuente, i, '"');

                if (seguidas >= 3)
                {
                    estado = Estado.TextoCrudo;
                    comillasCrudas = seguidas;
                    codigo = true;
                    i += seguidas;
                    continue;
                }

                estado = Literal(fuente, i) ? Estado.TextoLiteral : Estado.Texto;
                codigo = true;
                i++;
                continue;
            }

            if (c == '\'')
            {
                estado = Estado.Caracter;
                codigo = true;
                i++;
                continue;
            }

            if (!char.IsWhiteSpace(c))
            {
                codigo = true;
            }

            i++;
        }

        Cerrar();
        return lineas;
    }

    private static int Seguidas(string fuente, int desde, char c)
    {
        var n = 0;

        while (desde + n < fuente.Length && fuente[desde + n] == c)
        {
            n++;
        }

        return n;
    }

    /// <summary>Mira hacia atrás el prefijo del literal: <c>@"</c> y <c>$@"</c> escapan distinto que <c>"</c>.</summary>
    private static bool Literal(string fuente, int comilla)
    {
        for (var j = comilla - 1; j >= 0; j--)
        {
            if (fuente[j] == '@')
            {
                return true;
            }

            if (fuente[j] != '$')
            {
                return false;
            }
        }

        return false;
    }

    private enum Estado
    {
        Normal,
        ComentarioDeLinea,
        ComentarioDeBloque,
        Texto,
        TextoLiteral,
        TextoCrudo,
        Caracter,
    }
}
