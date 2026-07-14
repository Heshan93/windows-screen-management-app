using FluentAssertions;
using System.Text.Json;

namespace ScreenManager.Tests;

public sealed class P6ConnectivityPersistenceTests
{
    [Fact]
    public void ScreenStatusJson_RoundTrip_PreservesLastSuccessTimestamps()
    {
        var input = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase)
        {
            ["screen-a"] = new DateTimeOffset(2026, 7, 14, 11, 0, 0, TimeSpan.Zero),
            ["screen-b"] = new DateTimeOffset(2026, 7, 14, 11, 5, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(input);
        var output = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(json);

        output.Should().NotBeNull();
        output!["screen-a"].Should().Be(input["screen-a"]);
        output["screen-b"].Should().Be(input["screen-b"]);
    }
}
