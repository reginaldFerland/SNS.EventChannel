using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using ChannelExample;
using Event;
using GuidLookup;
using SyncExample;
using AsyncExample;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelExample.Benchmarks
{
    [MemoryDiagnoser]
    public class GuidGeneratorBenchmarks
    {
        [Benchmark(Description = "Generate one million GUIDs")]
        public List<Guid> GenerateOneMillionGuids() => GuidGenerator.GenerateOneMillionGuids();
    }

    [MemoryDiagnoser]
    public class SyncImplementationBenchmarks
    {
        private SyncImplementation _syncImplementation;
        private CancellationTokenSource _cts;

        [GlobalSetup]
        public void Setup()
        {
            _syncImplementation = new SyncImplementation(minDelayMs: 10, maxDelayMs: 100);
            _cts = new CancellationTokenSource();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _cts.Dispose();
        }

        [Benchmark(Description = "Individual processing with foreach")]
        public async Task HandleIndividually_Foreach()
        {
            await _syncImplementation.HandleIndividually_Foreach(_cts.Token);
        }

        [Benchmark(Description = "Individual processing with functional approach")]
        public async Task HandleIndividually_Functional()
        {
            await _syncImplementation.HandleIndividually_Functional(_cts.Token);
        }

        [Benchmark(Description = "Bulk processing with foreach")]
        public async Task HandleBulk_Foreach()
        {
            await _syncImplementation.HandleBulk_Foreach(_cts.Token);
        }

        [Benchmark(Description = "Bulk processing with functional approach")]
        public async Task HandleBulk_Functional()
        {
            await _syncImplementation.HandleBulk_Functional(_cts.Token);
        }
    }

    [MemoryDiagnoser]
    public class AsyncImplementationBenchmarks
    {
        private AsyncImplementation _asyncImplementation;
        private CancellationTokenSource _cts;
        private GuidChannel _guidChannel;

        [GlobalSetup]
        public void Setup()
        {
            _cts = new CancellationTokenSource();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Reset the channel for each iteration
            _guidChannel = new GuidChannel(boundedCapacity: 1_000_000, minDelayMs: 10, maxDelayMs: 100);
            _asyncImplementation = new AsyncImplementation(_guidChannel, minDelayMs: 10, maxDelayMs: 100);
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            _cts.Dispose();
        }

        [Benchmark(Description = "Individual processing with foreach")]
        public async Task HandleIndividually_Foreach()
        {
            await _asyncImplementation.HandleIndividually_Foreach(_cts.Token);
        }

        [Benchmark(Description = "Individual processing with functional approach")]
        public async Task HandleIndividually_Functional()
        {
            await _asyncImplementation.HandleIndividually_Functional(_cts.Token);
        }

        [Benchmark(Description = "Bulk processing with foreach")]
        public async Task HandleBulk_Foreach()
        {
            await _asyncImplementation.HandleBulk_Foreach(_cts.Token);
        }

        [Benchmark(Description = "Bulk processing with functional approach")]
        public async Task HandleBulk_Functional()
        {
            await _asyncImplementation.HandleBulk_Functional(_cts.Token);
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var config = DefaultConfig.Instance;

            // Comment out the benchmark you don't want to run
            // BenchmarkRunner.Run<GuidGeneratorBenchmarks>(config);
            BenchmarkRunner.Run<SyncImplementationBenchmarks>(config);
        }
    }
}
