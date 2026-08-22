```

BenchmarkDotNet v0.15.2, Linux KDE neon User Edition
Intel Core Ultra 7 155U 4.80GHz, 1 CPU, 14 logical and 12 physical cores
.NET SDK 9.0.118
  [Host]     : .NET 9.0.17 (9.0.1726.26416), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.17 (9.0.1726.26416), X64 RyuJIT AVX2


```
| Method             | EntityCount | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **DelegateField_Tick** | **500**         |   **3.283 μs** | **0.0083 μs** | **0.0069 μs** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| MethodGroup_Tick   | 500         |   3.327 μs | 0.0315 μs | 0.0280 μs |  1.01 |    0.01 | 0.0076 |      64 B |          NA |
| Action_Tick        | 500         |   2.572 μs | 0.0228 μs | 0.0190 μs |  0.78 |    0.01 |      - |         - |          NA |
|                    |             |            |           |           |       |         |        |           |             |
| **DelegateField_Tick** | **50000**       | **310.734 μs** | **5.1339 μs** | **4.8023 μs** |  **1.00** |    **0.02** |      **-** |         **-** |          **NA** |
| MethodGroup_Tick   | 50000       | 308.039 μs | 4.5769 μs | 4.2813 μs |  0.99 |    0.02 |      - |      64 B |          NA |
| Action_Tick        | 50000       | 248.119 μs | 3.4832 μs | 3.2582 μs |  0.80 |    0.02 |      - |         - |          NA |
