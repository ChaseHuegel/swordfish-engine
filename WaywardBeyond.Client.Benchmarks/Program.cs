using BenchmarkDotNet.Running;

new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
