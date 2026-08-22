using BenchmarkDotNet.Running;
using Swordfish.ECS.Benchmarks;

new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);