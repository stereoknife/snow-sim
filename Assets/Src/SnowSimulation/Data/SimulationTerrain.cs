using System;
using EasyButtons;
using HPML;
using TFM.Simulation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

using static Unity.Mathematics.math;

namespace TFM.SnowSimulation.Data
{
    [CreateAssetMenu(fileName = "SimTerrain", menuName = "Simulation Terrain", order = 0)]
    public class SimulationTerrain : ScriptableObject
    {
        public Texture2D heightmap;
        public double3 size;
    }
}