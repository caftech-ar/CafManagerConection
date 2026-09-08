using System.Net;

namespace CafManagerConection.Infrastructure.Tests.Actualizaciones;

public class ManejadorFalso : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    public HttpRequestMessage? UltimaPeticion { get; private set; }

    public static ManejadorFalso Fijo(HttpResponseMessage respuesta) => new(_ => respuesta);

    public static ManejadorFalso QueNuncaContesta() => new NuncaContesta();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        UltimaPeticion = request;
        return Task.FromResult(_responder(request));
    }

    private sealed class NuncaContesta() : ManejadorFalso(_ => throw new InvalidOperationException())
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("No debería llegar acá.");
        }
    }
}

public static class RespuestaHttp
{
    public static HttpResponseMessage Json(string json, HttpStatusCode codigo = HttpStatusCode.OK) =>
        new(codigo) { Content = new StringContent(json) };

    public static HttpResponseMessage Texto(string texto, HttpStatusCode codigo = HttpStatusCode.OK) =>
        new(codigo) { Content = new StringContent(texto) };

    public static HttpResponseMessage Bytes(byte[] datos, HttpStatusCode codigo = HttpStatusCode.OK)
    {
        var respuesta = new HttpResponseMessage(codigo) { Content = new ByteArrayContent(datos) };
        respuesta.Content.Headers.ContentLength = datos.Length;
        return respuesta;
    }

    public static HttpResponseMessage Estado(HttpStatusCode codigo) => new(codigo);
}
