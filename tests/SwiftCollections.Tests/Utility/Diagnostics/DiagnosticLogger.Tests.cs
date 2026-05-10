using SwiftCollections.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;
using Xunit;

namespace SwiftCollections.Tests.Utility.Diagnostics;

public class DiagnosticLoggerTests
{
    [Fact]
    public void Constructor_InitializesChannelsAndDefaults()
    {
        var logger = new TestDiagnosticLogger("Gameplay");

        Assert.Equal("Gameplay", logger.Name);
        Assert.Equal("Gameplay", logger.Channel.Name);
        Assert.Equal("Gameplay", logger.DebugChannel.Name);
        Assert.Equal(DiagnosticLevel.Warning, logger.MinimumLevel);
        Assert.False(logger.EnableDebugLogging);
        Assert.True(logger.Channel.IsEnabled(DiagnosticLevel.Warning));
        Assert.False(logger.DebugChannel.IsEnabled(DiagnosticLevel.Warning));
    }

    [Fact]
    public void EnableDebugLogging_GatesDebugChannelByMinimumLevel()
    {
        var logger = new TestDiagnosticLogger("Gameplay");
        var entries = new List<(DiagnosticLevel Level, string Message, string Source)>();
        logger.LogHandler = (level, message, source) => entries.Add((level, message, source));
        logger.MinimumLevel = DiagnosticLevel.Info;

        logger.DebugChannel.Write(DiagnosticLevel.Info, "hidden", "Debug");
        logger.EnableDebugLogging = true;
        logger.DebugChannel.Write(DiagnosticLevel.Info, "visible", "Debug");
        logger.MinimumLevel = DiagnosticLevel.Error;
        logger.DebugChannel.Write(DiagnosticLevel.Warning, "filtered", "Debug");

        Assert.Single(entries);
        Assert.Equal(DiagnosticLevel.Info, entries[0].Level);
        Assert.Equal("visible", entries[0].Message);
        Assert.Equal("Debug", entries[0].Source);
    }

    [Fact]
    public void LogHandlerAndFormatter_RestoreDefaultsWhenAssignedNull()
    {
        var logger = new TestDiagnosticLogger("Gameplay");

        logger.LogHandler = (level, message, source) => { };
        logger.CustomFormatter = (level, message, source) => $"{level}:{source}:{message}";

        logger.LogHandler = null;
        logger.CustomFormatter = null;

        Assert.NotNull(logger.LogHandler);
        Assert.NotNull(logger.CustomFormatter);
        Assert.Equal("[Warning] Gameplay.Source: message", logger.CustomFormatter(DiagnosticLevel.Warning, "message", "Source"));
    }

    [Fact]
    public void Channel_FallsBackToChannelNameForBlankSource()
    {
        var logger = new TestDiagnosticLogger("Gameplay");
        string capturedSource = string.Empty;
        logger.MinimumLevel = DiagnosticLevel.Info;
        logger.LogHandler = (level, message, source) => capturedSource = source;

        logger.Channel.Write(DiagnosticLevel.Info, "message");

        Assert.Equal("Gameplay", capturedSource);
    }

    [Fact]
    public void IsEnabled_DelegatesToChannelMinimumLevel()
    {
        var logger = new TestDiagnosticLogger("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Warning
        };

        Assert.False(logger.IsEnabled(DiagnosticLevel.Info));
        Assert.True(logger.IsEnabled(DiagnosticLevel.Warning));
    }

    [Fact]
    public void DefaultLogHandler_WritesInfoToOutputAndErrorsToError()
    {
        var logger = new TestDiagnosticLogger("Gameplay");
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);

            logger.DefaultLogHandler(DiagnosticLevel.Info, "loaded", "Systems");
            logger.DefaultLogHandler(DiagnosticLevel.Error, "failed", "Systems");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.Contains("[Info] Gameplay.Systems: loaded", output.ToString());
        Assert.Contains("[Error] Gameplay.Systems: failed", error.ToString());
    }

    [Fact]
    public void DefaultLogFormatter_UsesLoggerNameWhenSourceIsBlank()
    {
        var logger = new TestDiagnosticLogger("Gameplay");

        Assert.Equal("[Warning] Gameplay: message", logger.DefaultLogFormatter(DiagnosticLevel.Warning, "message", ""));
        Assert.Equal("[Warning] Gameplay: message", logger.DefaultLogFormatter(DiagnosticLevel.Warning, "message", "   "));
    }

    private sealed class TestDiagnosticLogger : DiagnosticLogger
    {
        public TestDiagnosticLogger(string channelName)
            : base(channelName)
        {
        }
    }
}
