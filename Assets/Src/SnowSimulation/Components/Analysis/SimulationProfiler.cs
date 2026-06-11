using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TFM.Solvers;
using UnityEngine;

namespace TFM.Components.Analysis
{
    public struct SimulationProfiler
    {
        public struct ProfilingRecord
        {
            public StochasticSimulation.EventId eventId;
            public float startTime, duration;
            public int ord;
            
            public string CSV => $"{ord},{eventId.ToString()},{startTime},{duration}";
        }

        public int MaxCapacity, AllocationChunk;
        public Dictionary<StochasticSimulation.EventId, List<ProfilingRecord>> records;

        public SimulationProfiler(int allocationChunk = 1024, int maxCapacity = -1)
        {
            MaxCapacity = maxCapacity * allocationChunk;
            AllocationChunk = allocationChunk;
            records = new Dictionary<StochasticSimulation.EventId, List<ProfilingRecord>>();
            foreach (StochasticSimulation.EventId value in Enum.GetValues(typeof(StochasticSimulation.EventId)))
            {
                records[value] = new List<ProfilingRecord>(AllocationChunk);
            }
        }
        
        public void AddRecord(StochasticSimulation.EventId eventId, float startTime, float duration)
        {
            var list = records[eventId];
            list.Add(new ProfilingRecord{eventId = eventId, startTime = startTime, duration = duration, ord = list.Count });

            if (MaxCapacity > 0 && list.Count >= MaxCapacity + AllocationChunk)
            {
                list.RemoveRange(0, 64);
            }
            else if (list.Count == list.Capacity)
            {
                list.Capacity += AllocationChunk;
            }
        }
        
        public List<ProfilingRecord> Records(StochasticSimulation.EventId eventId) => records[eventId];
        
        public List<ProfilingRecord> AllRecords()
        {
            var list = new List<ProfilingRecord>();
            foreach (var record in records.Values)
            {
                list.AddRange(record);
            }
            
            list.Sort((a, b) => Comparer<float>.Default.Compare(a.startTime, b.startTime));
            
            for (int i = 0; i < list.Count; i++)
            {
                var profilingRecord = list[i];
                profilingRecord.ord = i;
                list[i] = profilingRecord;
            }
            
            return list;
        }
        
        public Dictionary<StochasticSimulation.EventId, float> Averages()
        {
            var avg = new Dictionary<StochasticSimulation.EventId, float>();

            foreach (var (ev, list) in records)
            {
                avg[ev] = list.Sum(record => record.duration) / list.Count;
            }

            return avg;
        }
        
        public void WriteToFile(string filename, bool writeData = true, bool writeAverage = false)
        {
            using var writer = new StreamWriter(File.Open(filename, FileMode.Create));
            writer.WriteLine("number, event, time, duration");
            
            if (writeData) foreach (var record in AllRecords())
            {
                writer.WriteLine(record.CSV);
            }

            if (writeAverage) foreach (var (ev, avg) in Averages())
            {
                writer.WriteLine($"-1,{ev.ToString()} Average,-1,{avg}");
            }
        }
    }
}