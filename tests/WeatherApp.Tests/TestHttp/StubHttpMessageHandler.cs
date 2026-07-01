using System.Net;

namespace WeatherApp.Tests.TestHttp;

/// Replays a fixed response and captures the last request URI, so a typed
/// HttpClient really parses recorded bytes (real local I/O on the parse side).
/// An optional delay lets a test exercise the timeout / cancellation path — the
/// delay honours the cancellation token, so it fails closed rather than hangs.
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly TimeSpan _delay;
    public Uri? LastRequestUri { get; private set; }

    public StubHttpMessageHandler(HttpStatusCode status, string body, TimeSpan? delay = null)
    {
        _status = status;
        _body = body;
        _delay = delay ?? TimeSpan.Zero;
    }

    public static HttpClient ClientReturning(HttpStatusCode status, string body, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(status, body);
        return new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
    }

    /// A client whose response never arrives until the request is cancelled —
    /// used to prove the Geocoder's finite timeout / caller-cancellation fails closed.
    public static HttpClient ClientThatHangs(out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}", Timeout.InfiniteTimeSpan);
        return new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        if (_delay != TimeSpan.Zero)
            await Task.Delay(_delay, cancellationToken);
        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
