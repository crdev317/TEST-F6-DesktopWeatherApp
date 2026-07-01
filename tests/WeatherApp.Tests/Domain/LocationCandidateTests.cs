using FluentAssertions;
using WeatherApp.Core.Domain;

namespace WeatherApp.Tests.Domain;

public class LocationCandidateTests
{
    [Fact]
    public void Carries_its_fields()
    {
        var candidate = new LocationCandidate("London", "England", "United Kingdom", 51.50853, -0.12574);

        candidate.Name.Should().Be("London");
        candidate.Admin1.Should().Be("England");
        candidate.Country.Should().Be("United Kingdom");
        candidate.Latitude.Should().Be(51.50853);
        candidate.Longitude.Should().Be(-0.12574);
    }

    [Fact]
    public void Tolerates_a_null_admin1()
    {
        // The Geocoder omits the region (Admin1) for some places (Seam 1).
        var candidate = new LocationCandidate("Nowhere", null, "X", 1.0, 2.0);

        candidate.Admin1.Should().BeNull();
    }

    [Fact]
    public void Two_candidates_with_the_same_fields_are_equal()
    {
        var a = new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12);
        var b = new LocationCandidate("London", "England", "United Kingdom", 51.5, -0.12);

        a.Should().Be(b);
    }
}
