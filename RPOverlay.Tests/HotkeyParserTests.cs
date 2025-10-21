using RPOverlay.Core.Models;
using RPOverlay.Core.Utilities;
using Xunit;

namespace RPOverlay.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void TryParse_should_returnTrue_whenSingleKey()
    {
        var result = HotkeyParser.TryParse("F9", out var definition);

        Assert.True(result);
        Assert.NotNull(definition);
        Assert.Equal(HotkeyModifiers.None, definition!.Modifiers);
        Assert.Equal("F9", definition.Key);
    }

    [Fact]
    public void TryParse_should_returnTrue_whenModifiersPresent()
    {
        var result = HotkeyParser.TryParse("Ctrl+Shift+F8", out var definition);

        Assert.True(result);
        Assert.NotNull(definition);
        Assert.True(definition!.Modifiers.HasFlag(HotkeyModifiers.Control));
        Assert.True(definition.Modifiers.HasFlag(HotkeyModifiers.Shift));
        Assert.Equal("F8", definition.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Ctrl++")]
    public void TryParse_should_returnFalse_whenInputInvalid(string input)
    {
        var result = HotkeyParser.TryParse(input, out var definition);

        Assert.False(result);
        Assert.Null(definition);
    }
}
