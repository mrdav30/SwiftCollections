using SwiftCollections.Diagnostics;
using System;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Tests.Utility.Diagnostics;

public class DiagnosticChannelLoggingExtensionsTests
{
    [Fact]
    public void Info_DoesNotEvaluateFormattedExpressions_WhenDisabled()
    {
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Warning,
            Sink = (in DiagnosticEvent _) => throw new InvalidOperationException("Disabled diagnostics should not emit.")
        };
        int evaluationCount = 0;

        channel.Info($"expensive {Evaluate()}");

        Assert.Equal(0, evaluationCount);

        string Evaluate()
        {
            evaluationCount++;
            return "value";
        }
    }

    [Fact]
    public void Warn_EmitsDiagnosticWithResolvedCallerSource()
    {
        DiagnosticEvent captured = default;
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Info,
            Sink = (in DiagnosticEvent diagnostic) => captured = diagnostic
        };

        channel.Warn(
            $"value {42}",
            method: "Run",
            filePath: "/tmp/LoggerSource.cs");

        Assert.Equal("Gameplay", captured.Channel);
        Assert.Equal(DiagnosticLevel.Warning, captured.Level);
        Assert.Equal("value 42", captured.Message);
        Assert.Equal("LoggerSource.Run", captured.Source);
    }

    [Fact]
    public void Log_UsesDynamicLevelForEnablement()
    {
        var entries = new List<DiagnosticEvent>();
        var channel = new DiagnosticChannel("Gameplay")
        {
            MinimumLevel = DiagnosticLevel.Warning,
            Sink = (in DiagnosticEvent diagnostic) => entries.Add(diagnostic)
        };
        int evaluationCount = 0;

        channel.Log(DiagnosticLevel.Info, $"hidden {Evaluate()}");
        channel.Log(DiagnosticLevel.Error, $"visible {Evaluate()}", method: "Write", filePath: "/tmp/DynamicLogger.cs");

        DiagnosticEvent result = Assert.Single(entries);
        Assert.Equal(1, evaluationCount);
        Assert.Equal(DiagnosticLevel.Error, result.Level);
        Assert.Equal("visible value", result.Message);
        Assert.Equal("DynamicLogger.Write", result.Source);

        string Evaluate()
        {
            evaluationCount++;
            return "value";
        }
    }
}
