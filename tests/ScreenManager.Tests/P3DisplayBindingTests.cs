using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Config;
using OrderBuddy.ScreenManager.Core.Display;

namespace ScreenManager.Tests;

public sealed class P3DisplayBindingTests
{
    [Fact]
    public void BuildStableDisplayKey_SameInput_ReturnsSameKey()
    {
        var key1 = DisplayBindingPlanner.BuildStableDisplayKey("\\\\.\\DISPLAY1", 0, 0, 1920, 1080);
        var key2 = DisplayBindingPlanner.BuildStableDisplayKey("\\\\.\\DISPLAY1", 0, 0, 1920, 1080);

        key1.Should().Be(key2);
    }

    [Fact]
    public void Plan_BindsByDisplayKey_NotByOrdinal()
    {
        var display1 = new DisplayInfo(
            StableKey: DisplayBindingPlanner.BuildStableDisplayKey("DISPLAY1", 0, 0, 1920, 1080),
            DeviceName: "DISPLAY1",
            X: 0,
            Y: 0,
            Width: 1920,
            Height: 1080,
            WorkAreaWidth: 1920,
            WorkAreaHeight: 1040,
            Orientation: "Landscape",
            DpiScaleX: 1,
            DpiScaleY: 1,
            IsPrimary: true);

        var display2 = new DisplayInfo(
            StableKey: DisplayBindingPlanner.BuildStableDisplayKey("DISPLAY2", 1920, 0, 1080, 1920),
            DeviceName: "DISPLAY2",
            X: 1920,
            Y: 0,
            Width: 1080,
            Height: 1920,
            WorkAreaWidth: 1080,
            WorkAreaHeight: 1880,
            Orientation: "Portrait",
            DpiScaleX: 1,
            DpiScaleY: 1,
            IsPrimary: false);

        var screens = new List<ScreenConfig>
        {
            new()
            {
                Id = "s-till",
                Name = "Till",
                Enabled = true,
                DisplayKey = display1.StableKey,
                DisplayNumberHint = 2,
                Url = "https://till.example.org"
            },
            new()
            {
                Id = "s-kds",
                Name = "KDS",
                Enabled = true,
                DisplayKey = display2.StableKey,
                DisplayNumberHint = 1,
                Url = "https://kds.example.org"
            }
        };

        // Reordered list simulates display order change.
        var displays = new List<DisplayInfo> { display2, display1 };

        var bindings = DisplayBindingPlanner.Plan(screens, displays);

        bindings.Should().HaveCount(2);
        bindings.Single(b => b.Screen.Id == "s-till").Display!.StableKey.Should().Be(display1.StableKey);
        bindings.Single(b => b.Screen.Id == "s-kds").Display!.StableKey.Should().Be(display2.StableKey);
    }

    [Fact]
    public void Plan_MissingDisplayKey_MarksScreenAsWaiting()
    {
        var screen = new ScreenConfig
        {
            Id = "s-menu",
            Name = "Menu",
            Enabled = true,
            DisplayKey = "display:missing",
            DisplayNumberHint = 99,
            Url = "https://menu.example.org"
        };

        var displays = new List<DisplayInfo>
        {
            new(
                StableKey: DisplayBindingPlanner.BuildStableDisplayKey("DISPLAY1", 0, 0, 1920, 1080),
                DeviceName: "DISPLAY1",
                X: 0,
                Y: 0,
                Width: 1920,
                Height: 1080,
                WorkAreaWidth: 1920,
                WorkAreaHeight: 1040,
                Orientation: "Landscape",
                DpiScaleX: 1,
                DpiScaleY: 1,
                IsPrimary: true)
        };

        var binding = DisplayBindingPlanner.Plan([screen], displays).Single();

        binding.IsWaitingForDisplay.Should().BeTrue();
        binding.Display.Should().BeNull();
    }

    [Fact]
    public void Plan_RepluggedDisplay_RecoversBindingByStableKey()
    {
        var key = DisplayBindingPlanner.BuildStableDisplayKey("DISPLAY-KDS", 1920, 0, 1920, 1080);

        var screen = new ScreenConfig
        {
            Id = "screen-kds",
            Name = "KDS",
            Enabled = true,
            DisplayKey = key,
            DisplayNumberHint = 2,
            Url = "https://kds.example.org"
        };

        var noKdsDisplays = new List<DisplayInfo>
        {
            new(
                StableKey: DisplayBindingPlanner.BuildStableDisplayKey("DISPLAY1", 0, 0, 1920, 1080),
                DeviceName: "DISPLAY1",
                X: 0,
                Y: 0,
                Width: 1920,
                Height: 1080,
                WorkAreaWidth: 1920,
                WorkAreaHeight: 1040,
                Orientation: "Landscape",
                DpiScaleX: 1,
                DpiScaleY: 1,
                IsPrimary: true)
        };

        var waiting = DisplayBindingPlanner.Plan([screen], noKdsDisplays).Single();
        waiting.IsWaitingForDisplay.Should().BeTrue();

        var repluggedDisplays = new List<DisplayInfo>(noKdsDisplays)
        {
            new(
                StableKey: key,
                DeviceName: "DISPLAY-KDS",
                X: 1920,
                Y: 0,
                Width: 1920,
                Height: 1080,
                WorkAreaWidth: 1920,
                WorkAreaHeight: 1040,
                Orientation: "Landscape",
                DpiScaleX: 1,
                DpiScaleY: 1,
                IsPrimary: false)
        };

        var rebound = DisplayBindingPlanner.Plan([screen], repluggedDisplays).Single();
        rebound.IsWaitingForDisplay.Should().BeFalse();
        rebound.Display.Should().NotBeNull();
        rebound.Display!.StableKey.Should().Be(key);
    }
}
