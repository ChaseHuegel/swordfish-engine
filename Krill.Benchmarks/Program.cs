using BenchmarkDotNet.Running;
using Krill.Benchmarks;

new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);