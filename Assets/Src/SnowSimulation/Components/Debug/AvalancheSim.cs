using System;
using System.IO;
using EasyButtons;
using HPML;
using TFM.Components.Visualization;
using TFM.Simulation;
using TFM.SnowSimulation.Data;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;

namespace TFM.Components
{
    public class AvalancheSim : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private float simSpeed = 1;
        [SerializeField] private double4 startingSnow = new (0 , 0, 1, 0);
        [SerializeField] private int2 rupturePoint = new (249, 243);
        [SerializeField] private SimulationTerrain simulationTerrain;

        private doubleF _height;
        private double4F _snow;
        private NativeArray<double> _flow;
        private NativeArray<bool> _moving;
        private NativeArray<int4> _bounds;
        private Snow.Parameters _parameters = Snow.Parameters.Default;

        public doubleF Heightfield => _height;
        public double4F Snowfield => _snow;
        public NativeHashSet<int> SelectedPoints { get; }
        public NativeHashSet<int> HighlightedPoints { get; }
        
        private (float, double)[] _profilingData;

        private void Awake()
        {
            var terrain = GetComponent<SimulationTerrain>();
            var terrainSize = terrain.size;
            _height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, startingSnow);
            _flow = new NativeArray<double>(_height.Length * 9, Allocator.Persistent);
            _moving = new NativeArray<bool>(_height.Length, Allocator.Persistent);
            _bounds = new NativeArray<int4>(JobsUtility.JobWorkerCount + 1, Allocator.Persistent);
            _profilingData = new (float, double)[100];
        }

        [Button]
        private void Launch()
        {
            var f = clamp(rupturePoint - 1, 0, _snow.dimension);
            var t = clamp(rupturePoint + 2, 0, _snow.dimension);
            for (int i = f.x; i < t.x; i++)
            {
                for (int j = f.y; j < t.y; j++)
                {
                    _moving[_snow.index(i, j)] = true;
                }
            }

            stepn = 0;
        }

        private int stepn = 200;

        private void Update()
        {
            var preTime = DateTime.Now;
            Snow.AvalancheParallel(_snow, _flow, _height, _moving, Time.deltaTime * simSpeed, ref _parameters, default).Complete();
            var postTime = DateTime.Now;

            if (stepn == 100)
            {
                stepn++;
                var file = Path.Combine(Application.dataPath, "results", "avalanche_1024.csv");
                using var writer = new StreamWriter(File.Open(file, FileMode.Create));
                writer.WriteLine("number, event, time, duration");
                var totalDuration = 0d;
                for (int i = 0; i < _profilingData.Length; i++)
                {
                    var (time, duration) = _profilingData[i];
                    writer.WriteLine($"{i},{time},{duration}");
                }
                Debug.Log("Done");
            }
            else if (stepn < 100)
                _profilingData[stepn++] = (Time.unscaledTime, (postTime - preTime).TotalMilliseconds);
        }

        private void OnDestroy()
        {
            _height.Dispose();
            _snow.Dispose();
            _flow.Dispose();
            _moving.Dispose();
            _bounds.Dispose();
        }
    }
}