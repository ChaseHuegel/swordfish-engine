```

BenchmarkDotNet v0.15.2, Linux KDE neon User Edition
Intel Core Ultra 7 155U 4.80GHz, 1 CPU, 14 logical and 12 physical cores
.NET SDK 9.0.118
  [Host]     : .NET 9.0.17 (9.0.1726.26416), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.17 (9.0.1726.26416), X64 RyuJIT AVX2


```
| Method                               | Mean         | Error       | StdDev      | Gen0   | Allocated |
|------------------------------------- |-------------:|------------:|------------:|-------:|----------:|
| Query_SparseSingleComponent          |     137.0 ns |     2.42 ns |     2.02 ns |      - |         - |
| Query_SparseTwoComponents            |     204.3 ns |     2.69 ns |     2.52 ns |      - |         - |
| Query_DenseSingleComponent           |  41,090.7 ns |   523.54 ns |   464.11 ns |      - |         - |
| QueryParallel_DenseSingleComponent   | 120,591.1 ns | 2,258.92 ns | 2,218.56 ns | 0.7324 |    4653 B |
| Query_MixedRangesTwoComponents       |     143.5 ns |     1.97 ns |     1.85 ns |      - |         - |
| QueryRef_SparseSingleComponent       |     508.1 ns |     5.10 ns |     4.77 ns |      - |         - |
| QueryDirty_SparseSingleComponent     |     187.9 ns |     2.83 ns |     2.51 ns |      - |         - |
| Find_SparseSingleComponent           |     268.6 ns |     5.33 ns |     5.48 ns |      - |         - |
| QueryAction_SparseSingleComponent    |     138.0 ns |     2.12 ns |     1.98 ns |      - |         - |
| QueryAction_DenseSingleComponent     |  41,143.2 ns |   595.24 ns |   527.67 ns |      - |         - |
| QueryRefAction_SparseSingleComponent |     272.7 ns |     2.96 ns |     2.63 ns |      - |         - |
