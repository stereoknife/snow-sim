using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TFM.Solvers;
using UnityEngine;

namespace TFM.Components.Analysis
{
    public class SimulationProfiler
    {
        public struct EventRecord
        {
            public StochasticSimulation.EventId eventId;
            public float startTime;
            public double duration;
            public int ord;
            
            public string CSV => $"{ord},{eventId.ToString()},{startTime},{duration}";
        }
        
        public struct AverageRecord
        {
            public StochasticSimulation.EventId eventId;
            public double totalDuration, averageDuration;
            public int numEvents;
            
            public string CSV => $"{eventId.ToString()} Average,{numEvents},{totalDuration},{averageDuration}";
        }

        public int MaxCapacity, AllocationChunk;

        private bool fullRecordsValid = false;
        private bool averageRecordsValid = false;
        private readonly Dictionary<StochasticSimulation.EventId, List<EventRecord>> _records;
        private List<EventRecord> _fullRecords;
        private Dictionary<StochasticSimulation.EventId, AverageRecord> _averageRecords;

        public SimulationProfiler(int allocationChunk = 1024, int maxCapacity = -1)
        {
            MaxCapacity = maxCapacity * allocationChunk;
            _fullRecords = new List<EventRecord>();
            _averageRecords = new Dictionary<StochasticSimulation.EventId, AverageRecord>();
            _records = new Dictionary<StochasticSimulation.EventId, List<EventRecord>>();
            foreach (StochasticSimulation.EventId value in Enum.GetValues(typeof(StochasticSimulation.EventId)))
            {
                _records[value] = new List<EventRecord>();
            }
            Clear();
        }

        public void Clear()
        {
            fullRecordsValid = averageRecordsValid = false;
            foreach (var list in _records.Values)
            {
                list.Clear();
                list.Capacity = AllocationChunk;
            }
        }
        
        public void AddRecord(StochasticSimulation.EventId eventId, float startTime, double duration)
        {
            var list = _records[eventId];
            list.Add(new EventRecord{eventId = eventId, startTime = startTime, duration = duration, ord = list.Count });

            if (MaxCapacity > 0 && list.Count >= MaxCapacity + AllocationChunk)
            {
                list.RemoveRange(0, 64);
            }
            else if (list.Count == list.Capacity)
            {
                list.Capacity += AllocationChunk;
            }

            fullRecordsValid = averageRecordsValid = false;
        }
        
        public List<EventRecord> Records(StochasticSimulation.EventId eventId) => _records[eventId];
        
        public List<EventRecord> AllRecords()
        {
            if (fullRecordsValid) return _fullRecords;
            
            _fullRecords.Clear();
            _fullRecords.Capacity = _records.Values.Sum(l => l.Count);
            
            foreach (var record in _records.Values)
            {
                _fullRecords.AddRange(record);
            }
            
            _fullRecords.Sort((a, b) => Comparer<float>.Default.Compare(a.startTime, b.startTime));
            
            for (int i = 0; i < _fullRecords.Count; i++)
            {
                var profilingRecord = _fullRecords[i];
                profilingRecord.ord = i;
                _fullRecords[i] = profilingRecord;
            }

            fullRecordsValid = true;
            
            return _fullRecords;
        }
        
        public Dictionary<StochasticSimulation.EventId, AverageRecord> Averages()
        {
            if (averageRecordsValid) return _averageRecords;
            
            _averageRecords.Clear();
            foreach (var (ev, list) in _records)
            {
                var total = 0.0;
                var num = 0;

                foreach (var record in list)
                {
                    total += record.duration;
                    num++;
                }

                _averageRecords[ev] = new AverageRecord
                {
                    totalDuration = total,
                    numEvents = num,
                    averageDuration = total / num,
                    eventId = ev
                };
            }

            averageRecordsValid = true;
            return _averageRecords;
        }
        
        public void WriteToFile(string filename, bool writeData = true, bool writeAverage = false)
        {
            using var writer = new StreamWriter(File.Open(filename, FileMode.Create));
            writer.WriteLine("number, event, time, duration");
            
            if (writeData) foreach (var record in AllRecords())
            {
                writer.WriteLine(record.CSV);
            }

            if (writeAverage) foreach (var (ev, record) in Averages())
            {
                writer.WriteLine($"-1,{ev.ToString()} Average,-1,{record.averageDuration}");
            }
        }
    }
}