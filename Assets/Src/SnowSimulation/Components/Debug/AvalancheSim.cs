using System;
using HPML;
using TFM.Components.Solvers;
using TFM.Simulation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.Mathematics.math;

namespace TFM.Components
{
    public class AvalancheSim : MonoBehaviour, ISnowSimulation
    {
        [SerializeField] private float simSpeed = 1;
        
        private Terrain _terrain;

        private doubleF _height;
        private double4F _snow;
        private NativeArray<double> _flow;
        private NativeArray<bool> _moving;
        private Snow.Parameters _parameters = Snow.Parameters.Default;

        public doubleF Heightfield => _height;
        public double4F Snowfield => _snow;

        private void Awake()
        {
            var terrain = GetComponent<Terrain>();
            var terrainSize = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * terrain.units;
            _height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, double4(0, 1, 3, 0));
            _flow = new NativeArray<double>(_height.Length * 9, Allocator.Persistent);
            _moving = new NativeArray<bool>(_height.Length, Allocator.Persistent);
        }

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Space))
            {
                Debug.Log("Avalanche start");
                _moving[_height.index(249, 243)] = true;
                _moving[_height.index(249, 244)] = true;
                _moving[_height.index(249, 245)] = true;
                _moving[_height.index(250, 243)] = true;
                _moving[_height.index(250, 244)] = true;
                _moving[_height.index(250, 245)] = true;
                _moving[_height.index(251, 243)] = true;
                _moving[_height.index(251, 244)] = true;
                _moving[_height.index(251, 245)] = true;
            }
            
            Snow.AvalancheParallel(_snow, _flow, _height, _moving, Time.deltaTime * simSpeed, ref _parameters, default).Complete();
        }

        private void OnDestroy()
        {
            _height.Dispose();
            _snow.Dispose();
            _flow.Dispose();
            _moving.Dispose();
        }
    }
}