using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.ConsoleArguments;
using BenchmarkDotNet.ConsoleArguments.ListBenchmarks;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace SwiftCollections.Benchmarks;

internal static class Program
{
    private static readonly BenchmarkCatalog _catalog = BenchmarkCatalog.Create(typeof(Program).Assembly);

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return RunBenchmarks(BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly), args);
        }

        string command = args[0];

        if ((string.Equals(command, "list", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(command, "ls", StringComparison.OrdinalIgnoreCase)) &&
            args.Length == 1)
        {
            _catalog.WriteAvailableSelections(Console.Out);
            return 0;
        }

        if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase) && args.Length == 1)
        {
            WriteUsage();
            return 0;
        }

        if (string.Equals(command, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 1 && !args[1].StartsWith("-", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("The 'all' selection cannot be combined with other benchmark aliases.");
                Console.Error.WriteLine();
                WriteUsage();
                return 1;
            }

            return RunBenchmarks(BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly), args.Skip(1).ToArray());
        }

        int aliasCount = 0;
        while (aliasCount < args.Length && !args[aliasCount].StartsWith("-", StringComparison.Ordinal))
            aliasCount++;

        if (aliasCount == 0)
        {
            return RunBenchmarks(BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly), args);
        }

        Type[] selectedTypes = _catalog.Resolve(args.Take(aliasCount).ToArray(), out string unknownAlias);
        if (unknownAlias != null)
        {
            Console.Error.WriteLine($"Unknown benchmark selection '{unknownAlias}'.");
            Console.Error.WriteLine();
            WriteUsage();
            return 1;
        }

        return RunBenchmarks(BenchmarkSwitcher.FromTypes(selectedTypes), args.Skip(aliasCount).ToArray());
    }

    private static int RunBenchmarks(BenchmarkSwitcher switcher, string[] args)
    {
        var (parsed, _, options) = ConfigParser.Parse(args, ConsoleLogger.Default);
        if (!parsed)
            return ValidateHelpRequest(args);

        return GetExitCode(switcher.Run(args), options.PrintInformation ||
            options.ListBenchmarkCaseMode != ListBenchmarkCaseMode.Disabled);
    }

    private static bool IsHelpOrVersion(string arg) =>
        string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase);

    private static int ValidateHelpRequest(string[] args)
    {
        // BDN reports help/version as parse failures and can hide invalid accompanying options.
        var remaining = new List<string>();
        var temporaryFiles = new List<string>();
        bool removedHelp = false;
        try
        {
            foreach (string arg in args)
            {
                if (IsHelpOrVersion(arg))
                {
                    removedHelp = true;
                    continue;
                }

                string candidate = arg;
                if (arg.StartsWith("@", StringComparison.Ordinal))
                {
                    string[] lines = File.ReadAllLines(arg.Substring(1));
                    bool changed = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string stripped = StripResponseHelp(lines[i]);
                        changed |= stripped != lines[i];
                        lines[i] = stripped;
                    }

                    if (changed)
                    {
                        removedHelp = true;
                        string path = Path.GetTempFileName();
                        temporaryFiles.Add(path);
                        File.WriteAllLines(path, lines);
                        candidate = "@" + path;
                    }
                }
                remaining.Add(candidate);
            }

            if (!removedHelp)
                return 1;
            var (validOptions, _, _) = ConfigParser.Parse(remaining.ToArray(), ConsoleLogger.Default);
            return validOptions ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            foreach (string path in temporaryFiles)
                File.Delete(path);
        }
    }

    private static string StripResponseHelp(string line)
    {
        // Follow BDN's response-token quote/escape rules, retaining every other token verbatim.
        // Keeping response files intact also preserves BDN's single-pass @ expansion.
        var token = new StringBuilder();
        var result = new StringBuilder();
        bool quoted = false;
        int start = 0;
        int copied = 0;
        for (int i = 0; i <= line.Length; i++)
        {
            if (i == line.Length || (line[i] == ' ' && !quoted))
            {
                if (IsHelpOrVersion(token.ToString()))
                {
                    result.Append(line, copied, start - copied);
                    copied = i;
                }
                token.Clear();
                start = i + 1;
            }
            else if (line[i] == '"')
                quoted = !quoted;
            else if (line[i] == '\\' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                quoted = false;
                i++;
            }
            else if (line[i] == '\\' && quoted && i + 1 < line.Length && line[i + 1] == '\\')
                token.Append(line[++i]);
            else
                token.Append(line[i]);
        }
        return result.Append(line, copied, line.Length - copied).ToString();
    }

    private static int GetExitCode(IEnumerable<Summary> summaries, bool allowEmpty)
    {
        bool hasReports = false;
        foreach (Summary summary in summaries)
        {
            if (summary.HasCriticalValidationErrors || summary.Reports.IsEmpty)
                return 1;

            foreach (BenchmarkReport report in summary.Reports)
            {
                hasReports = true;
                // Earlier launches can succeed before a later child fails.
                if (!report.Success || report.ExecuteResults.Any(execution =>
                        !execution.IsSuccess || execution.ExitCode != 0))
                    return 1;
            }
        }

        return hasReports || allowEmpty ? 0 : 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0");
        Console.WriteLine("  dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- list");
        Console.WriteLine("  dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- dictionary-parity");
        Console.WriteLine("  dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- query --list flat");
        Console.WriteLine("  dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- set-workload --list flat");
        Console.WriteLine("  dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- all --list flat");
        Console.WriteLine();
        Console.WriteLine("Leading arguments that do not start with '-' are treated as benchmark selections.");
        Console.WriteLine("Remaining arguments are forwarded to BenchmarkDotNet.");
        Console.WriteLine();
        _catalog.WriteAvailableSelections(Console.Out);
    }
}
