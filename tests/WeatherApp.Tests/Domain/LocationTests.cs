using FluentAssertions;
using WeatherApp.Core.Domain;

namespace WeatherApp.Tests.Domain;

public class LocationTests
{
    [Fact]
    public void Carries_its_fields()
    {
        var location = new Location("Paris", 48.85, 2.35);

        location.Name.Should().Be("Paris");
        location.Latitude.Should().Be(48.85);
        location.Longitude.Should().Be(2.35);
    }

    [Fact]
    public void Two_locations_with_the_same_fields_are_equal()
    {
        new Location("Paris", 48.85, 2.35)
            .Should().Be(new Location("Paris", 48.85, 2.35));
    }
}
