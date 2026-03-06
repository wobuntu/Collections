using BenchmarkDotNet.Running;

// Run all:              dotnet run -c Release -- --filter *
// Run a single class:   dotnet run -c Release -- --filter *BulkInsert*
// Quick validation run: dotnet run -c Release -- --filter * --job short
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
