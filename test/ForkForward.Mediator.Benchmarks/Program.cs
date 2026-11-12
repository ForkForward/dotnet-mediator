using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace Benchmarks;

public class Program
{
    // Usual BenchmarkDotNet arguments work: --filter *Send*, --list flat, --artifacts <folder>.
    // Run it through Benchmark.ps1 to compare this build with the last published package.
    public static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, CreateConfig());
        return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
    }

    static IConfig CreateConfig() => DefaultConfig.Instance
        // A fixed, short measurement, so that several alternating rounds of baseline and current fit in a CI job.
        .AddJob(Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(4)
            .WithIterationCount(12)
            .WithIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(250)))
        // Allocated bytes do not depend on how busy the machine is, so they are the most reliable signal.
        .AddDiagnoser(MemoryDiagnoser.Default)
        // Benchmark.ps1 reads these files.
        .AddExporter(JsonExporter.Full)
        .WithOptions(ConfigOptions.JoinSummary);
}
