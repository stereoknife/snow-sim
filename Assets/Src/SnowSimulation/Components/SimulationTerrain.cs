using System;
using System.IO;
using System.Runtime.CompilerServices;
using HPML;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace TFM.Components
{
    public class SimulationTerrain : MonoBehaviour
    {
        [SerializeField] public Texture2D heightmap;
        [SerializeField] public float3 size;
        [SerializeField] public float units;
        [SerializeField] private TextAsset cache;

        private doubleF height;
        private doubleF temperature;
        private doubleF windAltitude;
        private double2F wind;
    }
}
