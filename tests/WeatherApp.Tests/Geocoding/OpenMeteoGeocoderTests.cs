using System.Net;
using FluentAssertions;
using WeatherApp.Core.Geocoding;
using WeatherApp.Tests.TestHttp;

namespace WeatherApp.Tests.Geocoding;

public class OpenMeteoGeocoderTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public async Task Parses_multiple_candidates_with_fields()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-london.json"), out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var result = await geocoder.Search("London", CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("London");
        result[0].Country.Should().Be("United Kingdom");
        result[0].Admin1.Should().Be("England");
        result[0].Latitude.Should().BeApproximately(51.50853, 0.0001);
    }

    [Fact]
    public async Task Treats_absent_results_key_as_empty_list()
    {
        // Seam 1: a zero-match query omits the `results` key entirely.
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-zero.json"), out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var result = await geocoder.Search("zzzznotaplace", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Tolerates_absent_admin1()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK,
            """{"results":[{"name":"Nowhere","latitude":1.0,"longitude":2.0,"country":"X"}]}""", out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var result = await geocoder.Search("Nowhere", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Admin1.Should().BeNull();
    }

    [Fact]
    public async Task Sends_name_and_count_params()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-single.json"), out var handler);
        var geocoder = new OpenMeteoGeocoder(http);

        await geocoder.Search("Paris", CancellationToken.None);

        handler.LastRequestUri!.Query.Should().Contain("name=Paris").And.Contain("count=");
    }

    [Fact]
    public async Task Throws_on_error_status()
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.BadRequest, Fixture("geocoding-error-400.json"), out _);
        var geocoder = new OpenMeteoGeocoder(http);

        var act = () => geocoder.Search("", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Security AC1: a query with query-significant characters must be transmitted
    // as a single URL-encoded `name` value — never letting the caller inject or
    // forge extra query parameters.
    [Theory]
    [InlineData("London&count=999")]
    [InlineData("Paris#fragment")]
    [InlineData("Berlin\r\nHost: evil")]
    public async Task Url_encodes_query_so_no_forged_params_are_injected(string malicious)
    {
        var http = StubHttpMessageHandler.ClientReturning(HttpStatusCode.OK, Fixture("geocoding-single.json"), out var handler);
        var geocoder = new OpenMeteoGeocoder(http);

        await geocoder.Search(malicious, CancellationToken.None);

        var query = handler.LastRequestUri!.Query;
        // The whole query travels as one encoded `name` value...
        query.Should().Contain($"name={Uri.EscapeDataString(malicious)}");
        // ...the legitimate count is intact, with no forged param and no fragment/CRLF leak.
        query.Should().Contain("count=10");
        query.Should().NotContain("count=999");
        query.Should().NotContain("#fragment");
        query.Should().NotContain("\r").And.NotContain("\n");
    }

    // Security AC2: a call whose CancellationToken is cancelled fails closed to the
    // caller's error path — never an unhandled hang.
    [Fact]
    public async Task Cancelled_token_fails_closed()
    {
        var http = StubHttpMessageHandler.ClientThatHangs(out _);
        var geocoder = new OpenMeteoGeocoder(http);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => geocoder.Search("London", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Security AC2: a call that exceeds the Geocoder's finite timeout fails closed
    // rather than hanging indefinitely.
    [Fact]
    public async Task Exceeding_the_finite_timeout_fails_closed()
    {
        var http = StubHttpMessageHandler.ClientThatHangs(out _);
        var geocoder = new OpenMeteoGeocoder(http, timeout: TimeSpan.FromMilliseconds(50));

        var act = () => geocoder.Search("London", CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
