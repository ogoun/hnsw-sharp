﻿// <copyright file="Program.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace HNSW.Net.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading.Tasks;

    using Parameters = SmallWorld<float[], float>.Parameters;

    /// <summary>
    /// The demo program.
    /// </summary>
    public static class Program
    {
        private const int SampleSize = 1_000;
        private const int SampleIncrSize = 50;
        private const int TestSize = 10 * SampleSize;
        private const int Dimensionality = 16;
        private const string VectorsPathSuffix = "vectors.hnsw";
        private const string GraphPathSuffix = "graph.hnsw";

        /// <summary>
        /// Entry point.
        /// </summary>
        public static void Main()
        {
            BuildAndSave("random");
            LoadAndSearch("random");
        }

        private static void BuildAndSave(string pathPrefix)
        {
            Stopwatch clock;
            List<float[]> sampleVectors;

            var parameters = new Parameters();
            parameters.EnableDistanceCacheForConstruction = false;

            var world = new SmallWorld<float[], float>(CosineDistance.SIMDForUnits, DefaultRandomGenerator.Instance, parameters);

            Console.Write($"Generating {SampleSize} sample vectos... ");
            clock = Stopwatch.StartNew();
            sampleVectors = RandomVectors(Dimensionality, SampleSize);
            Console.WriteLine($"Done in {clock.ElapsedMilliseconds} ms.");

            Console.WriteLine("Building HNSW graph... ");
            using (var listener = new MetricsEventListener(EventSources.GraphBuildEventSource.Instance))
            {
                clock = Stopwatch.StartNew();
                for(int i = 0; i < (SampleSize / SampleIncrSize); i++)
                {
                    world.AddItems(sampleVectors.Skip(i * SampleIncrSize).Take(SampleIncrSize).ToArray());
                    Console.WriteLine($"\nAt {i} of {SampleSize / SampleIncrSize}  Elapsed: {clock.ElapsedMilliseconds} ms.\n");
                }
                Console.WriteLine($"Done in {clock.ElapsedMilliseconds} ms.");
            }

            Console.Write($"Saving HNSW graph to '${Path.Combine(Directory.GetCurrentDirectory(), pathPrefix)}'... ");
            clock = Stopwatch.StartNew();
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream sampleVectorsStream = new MemoryStream();
            formatter.Serialize(sampleVectorsStream, sampleVectors);
            File.WriteAllBytes($"{pathPrefix}.{VectorsPathSuffix}", sampleVectorsStream.ToArray());
            File.WriteAllBytes($"{pathPrefix}.{GraphPathSuffix}", world.SerializeGraph());
            Console.WriteLine($"Done in {clock.ElapsedMilliseconds} ms.");
        }

        private static void LoadAndSearch(string pathPrefix)
        {
            Stopwatch clock;

            Console.Write("Loading HNSW graph... ");
            clock = Stopwatch.StartNew();
            BinaryFormatter formatter = new BinaryFormatter();
            var sampleVectors = (List<float[]>)formatter.Deserialize(new MemoryStream(File.ReadAllBytes($"{pathPrefix}.{VectorsPathSuffix}")));
            var world = SmallWorld<float[], float>.DeserializeGraph(sampleVectors, CosineDistance.NonOptimized, DefaultRandomGenerator.Instance, File.ReadAllBytes($"{pathPrefix}.{GraphPathSuffix}"));

            Console.WriteLine($"Done in {clock.ElapsedMilliseconds} ms.");

            Console.Write($"Generating {TestSize} test vectos... ");
            clock = Stopwatch.StartNew();
            var vectors = RandomVectors(Dimensionality, TestSize);
            Console.WriteLine($"Done in {clock.ElapsedMilliseconds} ms.");

            Console.WriteLine("Running search agains the graph... ");
            using (var listener = new MetricsEventListener(EventSources.GraphSearchEventSource.Instance))
            {
                clock = Stopwatch.StartNew();
                Parallel.ForEach(vectors, (vector) =>
                {
                    world.KNNSearch(vector, 10);
                });
                Console.WriteLine($"Done in {clock.ElapsedMilliseconds} ms.");
            }
        }

        private static List<float[]> RandomVectors(int vectorSize, int vectorsCount)
        {
            var random = new Random(42);
            var vectors = new List<float[]>();

            for (int i = 0; i < vectorsCount; i++)
            {
                var vector = new float[vectorSize];
                DefaultRandomGenerator.Instance.NextFloats(vector);
                VectorUtils.NormalizeSIMD(vector);
                vectors.Add(vector);
            }

            return vectors;
        }

        private class MetricsEventListener : EventListener
        {
            private readonly EventSource eventSource;

            public MetricsEventListener(EventSource eventSource)
            {
                this.eventSource = eventSource;
                EnableEvents(this.eventSource, EventLevel.LogAlways, EventKeywords.All, new Dictionary<string, string> { { "EventCounterIntervalSec", "1" } });
            }

            public override void Dispose()
            {
                DisableEvents(eventSource);
                base.Dispose();
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                var counterData = eventData.Payload?.FirstOrDefault() as IDictionary<string, object>;
                if (counterData?.Count == 0)
                {
                    return;
                }

                Console.WriteLine($"[{counterData["Name"]}]: Avg={counterData["Mean"]}; SD={counterData["StandardDeviation"]}; Count={counterData["Count"]}");
            }
        }
    }
}
