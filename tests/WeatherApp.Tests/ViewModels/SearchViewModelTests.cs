using FluentAssertions;
using WeatherApp.Core.Domain;
using WeatherApp.Core.ViewModels;
using WeatherApp.Tests.Fakes;

namespace WeatherApp.Tests.ViewModels;

public class SearchViewModelTests
{
    private static (SearchViewModel vm, FakeGeocoder geo, ManualDebounceScheduler sched) Build()
    {
        var geo = new FakeGeocoder();
        var sched = new ManualDebounceScheduler();
        return (new SearchViewModel(geo, sched), geo, sched);
    }

    [Fact]
    public void Query_under_two_chars_schedules_no_search_and_clears()
    {
        var (vm, _, sched) = Build();
        vm.Candidates.Add(new LocationCandidate("Old", null, "X", 0, 0));

        vm.Query = "a";

        sched.ScheduleCount.Should().Be(0);
        vm.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_populates_candidates()
    {
        var (vm, geo, sched) = Build();
        vm.Query = "London";

        var fire = sched.FireAsync();
        geo.Complete("London", new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12));
        await fire;

        vm.Candidates.Should().ContainSingle().Which.Name.Should().Be("London");
        vm.SearchMessage.Should().BeNull();
    }

    [Fact]
    public async Task Zero_results_sets_message()
    {
        var (vm, geo, sched) = Build();
        vm.Query = "zzzz";

        var fire = sched.FireAsync();
        geo.Complete("zzzz"); // empty
        await fire;

        vm.Candidates.Should().BeEmpty();
        vm.SearchMessage.Should().Contain("No places found");
    }

    [Fact]
    public async Task Geocoder_failure_sets_error_message()
    {
        var (vm, geo, sched) = Build();
        vm.Query = "London";

        var fire = sched.FireAsync();
        geo.Fail("London", new HttpRequestException("boom"));
        await fire;

        vm.SearchMessage.Should().Contain("Couldn't search");
    }

    [Fact]
    public async Task Stale_response_is_dropped_latest_query_wins()
    {
        var (vm, geo, sched) = Build();

        vm.Query = "Lon";
        var fire1 = sched.FireAsync();   // search seq 1 in flight for "Lon"
        vm.Query = "Lond";
        var fire2 = sched.FireAsync();   // search seq 2 in flight for "Lond"

        // Complete the LATEST first, then the stale earlier one.
        geo.Complete("Lond", new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12));
        await fire2;
        geo.Complete("Lon", new LocationCandidate("Longview", "Texas", "United States", 32.5, -94.7));
        await fire1;

        vm.Candidates.Should().ContainSingle().Which.Admin1.Should().Be("England");
    }

    [Fact]
    public void Selecting_a_candidate_raises_LocationSelected()
    {
        var (vm, _, _) = Build();
        Location? selected = null;
        vm.LocationSelected += loc => selected = loc;

        vm.SelectCommand.Execute(new LocationCandidate("Paris", "Île-de-France", "France", 48.85, 2.35));

        selected.Should().Be(new Location("Paris", 48.85, 2.35));
    }

    // The query is embedded in every induced exception so the test can prove the
    // surfaced copy never echoes it (the request URL carries the query).
    private const string SensitiveQuery = "Londonsecret";
    private static readonly string RequestUrl =
        $"https://geocoding-api.open-meteo.com/v1/search?name={SensitiveQuery}&count=10";

    public static IEnumerable<object[]> FailureModes() => new[]
    {
        // network / HTTP — message carries the request URL (and thus the query)
        new object[] { new HttpRequestException($"No connection to {RequestUrl}") },
        // timeout
        new object[] { new TaskCanceledException($"The request to {RequestUrl} timed out") },
        // JSON parse failure — message carries the query too
        new object[] { new System.Text.Json.JsonException($"Unexpected token while parsing response for {SensitiveQuery}") },
        // any other exception
        new object[] { new InvalidOperationException($"boom for {SensitiveQuery} at {RequestUrl}") },
    };

    [Theory]
    [MemberData(nameof(FailureModes))]
    public async Task Failure_message_is_neutral_and_leaks_no_details(Exception failure)
    {
        var (vm, geo, sched) = Build();
        vm.Query = SensitiveQuery;

        var fire = sched.FireAsync();
        geo.Fail(SensitiveQuery, failure);
        await fire;

        vm.SearchMessage.Should().Be("Couldn't search right now — check your connection and try again.");
        vm.SearchMessage.Should().NotContain(SensitiveQuery);          // no query echo
        vm.SearchMessage.Should().NotContain("http");                  // no request URL
        vm.SearchMessage.Should().NotContain("Exception");             // no exception type name
        vm.SearchMessage.Should().NotContain(failure.GetType().Name);  // no exception type name
        vm.SearchMessage.Should().NotContain(" at ");                  // no stack-trace frame
    }
}
