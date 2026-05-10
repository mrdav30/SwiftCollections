using SwiftCollections.Diagnostics;
using System;
using Xunit;

namespace SwiftCollections.Tests.Utility.Diagnostics;

public class DiagnosticInterpolatedStringHandlerTests
{
    [Fact]
    public void AppendFormatted_WhenEnabled_FormatsSupportedValueShapes()
    {
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info
        };
        var handler = new DiagnosticInterpolatedStringHandler(0, 0, channel, DiagnosticLevel.Info, out bool isEnabled);

        handler.AppendLiteral("value ");
        handler.AppendFormatted(15);
        handler.AppendLiteral(" hex ");
        handler.AppendFormatted(15, "X2");
        handler.AppendLiteral(" aligned ");
        handler.AppendFormatted(7, 3);
        handler.AppendLiteral(" both ");
        handler.AppendFormatted(15, -4, "X");
        handler.AppendLiteral(" string ");
        handler.AppendFormatted("ok");
        handler.AppendLiteral(" null ");
        handler.AppendFormatted((string)null);
        handler.AppendLiteral(" right ");
        handler.AppendFormatted("x", 3);
        handler.AppendLiteral(" left ");
        handler.AppendFormatted("y", -3, null);
        handler.AppendLiteral(" span ");
        handler.AppendFormatted("go".AsSpan());
        handler.AppendLiteral(" spanRight ");
        handler.AppendFormatted("hi".AsSpan(), 4);
        handler.AppendLiteral(" spanLeft ");
        handler.AppendFormatted("z".AsSpan(), -3, null);

        string text = handler.GetFormattedText();

        Assert.True(isEnabled);
        Assert.Contains("value 15", text);
        Assert.Contains("hex 0F", text);
        Assert.Contains("aligned   7", text);
        Assert.Contains("both F   ", text);
        Assert.Contains("string ok", text);
        Assert.Contains("null ", text);
        Assert.Contains("right   x", text);
        Assert.Contains("left y  ", text);
        Assert.Contains("span go", text);
        Assert.Contains("spanRight   hi", text);
        Assert.EndsWith("spanLeft z  ", text);
    }

    [Fact]
    public void AppendFormatted_WhenDisabled_IgnoresAllSegments()
    {
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Error
        };
        var handler = new DiagnosticInterpolatedStringHandler(0, 0, channel, DiagnosticLevel.Info, out bool isEnabled);

        handler.AppendLiteral("hidden");
        handler.AppendFormatted(15);
        handler.AppendFormatted(15, "X2");
        handler.AppendFormatted(7, 3);
        handler.AppendFormatted(15, -4, "X");
        handler.AppendFormatted("ok");
        handler.AppendFormatted("x", 3);
        handler.AppendFormatted("y", -3, null);
        handler.AppendFormatted("go".AsSpan());
        handler.AppendFormatted("hi".AsSpan(), 4);
        handler.AppendFormatted("z".AsSpan(), -3, null);

        Assert.False(isEnabled);
        Assert.False(handler.IsEnabled);
        Assert.Equal(string.Empty, handler.GetFormattedText());
    }

    [Fact]
    public void Constructor_WithNullChannel_DisablesFormatting()
    {
        var handler = new DiagnosticInterpolatedStringHandler(0, 0, null, DiagnosticLevel.Info, out bool isEnabled);

        handler.AppendLiteral("hidden");

        Assert.False(isEnabled);
        Assert.False(handler.IsEnabled);
        Assert.Equal(string.Empty, handler.GetFormattedText());
    }

    [Fact]
    public void DiagnosticMessageHandler_ForwardsAllGenericFormattingOverloads()
    {
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info
        };
        var handler = new DiagnosticMessageHandler<InfoDiagnosticLevel>(0, 0, channel, out bool isEnabled);

        handler.AppendLiteral("value ");
        handler.AppendFormatted(15);
        handler.AppendLiteral(" hex ");
        handler.AppendFormatted(15, "X2");
        handler.AppendLiteral(" aligned ");
        handler.AppendFormatted(7, 3);
        handler.AppendLiteral(" both ");
        handler.AppendFormatted(15, -4, "X");

        string text = handler.Message.GetFormattedText();

        Assert.True(isEnabled);
        Assert.Contains("value 15", text);
        Assert.Contains("hex 0F", text);
        Assert.Contains("aligned   7", text);
        Assert.Contains("both F   ", text);
    }

    [Fact]
    public void DiagnosticMessageHandler_RejectsNullChannel()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagnosticMessageHandler<InfoDiagnosticLevel>(0, 0, null, out _));
        Assert.Throws<ArgumentNullException>(() => new DiagnosticMessageHandler<DynamicDiagnosticLevel>(0, 0, null, DiagnosticLevel.Info, out _));
    }
}
