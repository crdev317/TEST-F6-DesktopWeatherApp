using FluentAssertions;
using WeatherApp.Core.Domain;

namespace WeatherApp.Tests.Domain;

public class CurrentConditionsTests
{
    [Fact]
    public void Carries_its_fields()
    {
        var conditions = new CurrentConditions(20.4, 12.2, "Mainly clear");

        conditions.TemperatureC.Should().Be(20.4);
        conditions.WindSpeedKmh.Should().Be(12.2);
        conditions.Condition.Should().Be("Mainly clear");
    }

    [Fact]
    public void Two_snapshots_with_the_same_fields_are_equal()
    {
        new CurrentConditions(20.4, 12.2, "Mainly clear")
            .Should().Be(new CurrentConditions(20.4, 12.2, "Mainly clear"));
    }
}
