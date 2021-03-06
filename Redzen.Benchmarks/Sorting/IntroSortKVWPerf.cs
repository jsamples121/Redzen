﻿using System;
using Redzen.Sorting;

namespace Redzen.Benchmarks.Sorting
{
    /// <summary>
    /// Performance benchmarks for <see cref="IntroSort{K, V, W}"/>.
    /// 
    /// The benchmarks are:
    /// 
    ///    IntroSort<int,int,int>.Sort() [Random] - Performance sorting pure random data.
    /// 
    ///    IntroSort<int,int,int>.Sort() [Natural] - Performance sorting 'natural' data, i.e., with sub-spans of
    ///    already sorted data, some in the wrong order (ascending vs. descending).
    /// 
    /// </summary>
    internal sealed class IntroSortKVWPerf
    {
        #region Public Static Methods

        public static void RunBenchmarks(
            int length, int loopsPerRun)
        {
            RunBenchmark_Random(length, loopsPerRun);
            RunBenchmark_Natural(length, loopsPerRun);
        }

        #endregion

        #region Private Static Methods [Benchmarks]

        private static void RunBenchmark_Random(
            int length, int loopsPerRun)
        {
            var benchmark = new SpanSortKVWPerf(
                SpanSortPerfUtils.InitRandom,
                IntroSort<int,int,int>.Sort,
                length,
                loopsPerRun);

            double msPerSort = benchmark.Run();
            Console.WriteLine($"IntroSort<int,int,int>.Sort() [Random]:\t\t{msPerSort} ms / sort");
        }

        private static void RunBenchmark_Natural(
            int length, int loopsPerRun)
        {
            var benchmark = new SpanSortKVWPerf(
                SpanSortPerfUtils.InitNatural,
                IntroSort<int,int,int>.Sort,
                length,
                loopsPerRun);

            double msPerSort = benchmark.Run();
            Console.WriteLine($"IntroSort<int,int,int>.Sort() [Natural]:\t{msPerSort} ms / sort");
        }

        #endregion
    }
}
