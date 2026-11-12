using BenchmarkDotNet.Running;

namespace ForkForward.Mediator.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}