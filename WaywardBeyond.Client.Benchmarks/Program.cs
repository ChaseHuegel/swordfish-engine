using BenchmarkDotNet.Running;
using WaywardBeyond.Client.Benchmarks;

new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
