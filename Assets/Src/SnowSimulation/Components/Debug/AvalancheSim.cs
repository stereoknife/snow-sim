using EasyButtons;
using HPML;
using TFM.Components.Visualization;
using TFM.Simulation;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace TFM.Components
{
    public class AvalancheSim : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private float simSpeed = 1;
        [SerializeField] private double4 startingSnow = new double4(0 , 0, 1, 0);
        [SerializeField] private int2 rupturePoint = new int2(249, 243);
        
        private Terrain _terrain;

        private doubleF _height;
        private double4F _snow;
        private NativeArray<double> _flow;
        private NativeArray<bool> _moving;
        private NativeArray<int4> _bounds;
        private Snow.Parameters _parameters = Snow.Parameters.Default;

        public doubleF Heightfield => _height;
        public double4F Snowfield => _snow;

        private void Awake()
        {
            var terrain = GetComponent<Terrain>();
            var terrainSize = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * terrain.units;
            _height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, startingSnow);
            _flow = new NativeArray<double>(_height.Length * 9, Allocator.Persistent);
            _moving = new NativeArray<bool>(_height.Length, Allocator.Persistent);
            _bounds = new NativeArray<int4>(JobsUtility.JobWorkerCount + 1, Allocator.Persistent);
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
        }

        private void Update()
        {
            Snow.AvalancheParallel(_snow, _flow, _height, _moving, Time.deltaTime * simSpeed, ref _parameters, default).Complete();
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