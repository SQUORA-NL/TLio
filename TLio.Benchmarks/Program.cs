using BenchmarkDotNet.Running;
using TLio.Benchmarks;

if (BigDocumentGcComparison.Handles(args))
    return BigDocumentGcComparison.Run(args);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
