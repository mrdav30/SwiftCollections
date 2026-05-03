using SwiftCollections.Diagnostics;
using System;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Tests;

public class DiagnosticChannelTests
{
    [Fact]
    public void Constructor_NormalizesNameAndStartsDisabled()
    {
        var namedChannel = new DiagnosticChannel("Gameplay");
        var whitespaceChannel = new DiagnosticChannel("   ");
        var nullChannel = new DiagnosticChannel(null);

        Assert.Equal("Gameplay", namedChannel.Name);
        Assert.Equal("Default", whitespaceChannel.Name);
        Assert.Equal("Default", nullChannel.Name);
        Assert.Equal(DiagnosticLevel.None, namedChannel.MinimumLevel);
        Assert.False(namedChannel.IsEnabled(DiagnosticLevel.None));
        Assert.False(namedChannel.IsEnabled(DiagnosticLevel.Info));
        Assert.NotNull(namedChannel.Sink);
    }

    [Fact]
    public void IsEnabled_RespectsMinimumLevel()
    {
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Warning
        };

        Assert.False(channel.IsEnabled(DiagnosticLevel.None));
        Assert.False(channel.IsEnabled(DiagnosticLevel.Info));
        Assert.True(channel.IsEnabled(DiagnosticLevel.Warning));
        Assert.True(channel.IsEnabled(DiagnosticLevel.Error));
    }

    [Fact]
    public void Write_EmitsDiagnosticsWhenEnabled()
    {
        var events = new List<DiagnosticEvent>();

        void Capture(in DiagnosticEvent diagnostic) => events.Add(diagnostic);

        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info,
            Sink = Capture
        };

        channel.Write(DiagnosticLevel.Warning, "Resized buffer.", "SwiftList");

        DiagnosticEvent diagnostic = Assert.Single(events);
        Assert.Equal("Gameplay", diagnostic.Channel);
        Assert.Equal(DiagnosticLevel.Warning, diagnostic.Level);
        Assert.Equal("Resized buffer.", diagnostic.Message);
        Assert.Equal("SwiftList", diagnostic.Source);
    }

    [Fact]
    public void Write_InterpolatedMessageDoesNotEvaluateFormattedExpressionsWhenDisabled()
    {
        int evaluations = 0;
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Error
        };

        string SideEffect()
        {
            evaluations++;
            return "evaluated";
        }

        channel.Write(DiagnosticLevel.Warning, $"Skipped {SideEffect()}.", "Tests");

        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void Write_InterpolatedMessageEmitsDiagnosticsWhenEnabled()
    {
        var events = new List<DiagnosticEvent>();

        void Capture(in DiagnosticEvent diagnostic) => events.Add(diagnostic);

        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info,
            Sink = Capture
        };

        channel.Write(DiagnosticLevel.Warning, $"Resized buffer to {128} entries.", "SwiftList");

        DiagnosticEvent diagnostic = Assert.Single(events);
        Assert.Equal("Gameplay", diagnostic.Channel);
        Assert.Equal(DiagnosticLevel.Warning, diagnostic.Level);
        Assert.Equal("Resized buffer to 128 entries.", diagnostic.Message);
        Assert.Equal("SwiftList", diagnostic.Source);
    }

    [Fact]
    public void Write_StringMessageRemainsSupported()
    {
        var events = new List<DiagnosticEvent>();

        void Capture(in DiagnosticEvent diagnostic) => events.Add(diagnostic);

        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info,
            Sink = Capture
        };

        string message = "Existing string API.";

        channel.Write(DiagnosticLevel.Info, message, "Tests");

        DiagnosticEvent diagnostic = Assert.Single(events);
        Assert.Equal(DiagnosticLevel.Info, diagnostic.Level);
        Assert.Equal(message, diagnostic.Message);
        Assert.Equal("Tests", diagnostic.Source);
    }

    [Fact]
    public void Write_IgnoresDisabledLevels()
    {
        var events = new List<DiagnosticEvent>();

        void Capture(in DiagnosticEvent diagnostic) => events.Add(diagnostic);

        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Error,
            Sink = Capture
        };

        channel.Write(DiagnosticLevel.None, "Ignored none.", "Tests");
        channel.Write(DiagnosticLevel.Warning, "Ignored warning.", "Tests");

        Assert.Empty(events);
    }

    [Fact]
    public void Sink_NullAssignmentFallsBackToNoop()
    {
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info,
            Sink = null
        };

        Exception exception = Record.Exception(() => channel.Write(DiagnosticLevel.Info, "Handled.", "Tests"));

        Assert.Null(exception);
        Assert.NotNull(channel.Sink);
    }

    [Fact]
    public void DiagnosticEvent_NormalizesNullValues()
    {
        var diagnostic = new DiagnosticEvent(null, DiagnosticLevel.Error, null, null);

        Assert.Equal(string.Empty, diagnostic.Channel);
        Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        Assert.Equal(string.Empty, diagnostic.Message);
        Assert.Equal(string.Empty, diagnostic.Source);
    }

    [Fact]
    public void Shared_ReturnsSingletonSwiftCollectionsChannel()
    {
        Assert.Same(SwiftCollectionDiagnostics.Shared, SwiftCollectionDiagnostics.Shared);
        Assert.Equal("SwiftCollections", SwiftCollectionDiagnostics.Shared.Name);
    }
}
