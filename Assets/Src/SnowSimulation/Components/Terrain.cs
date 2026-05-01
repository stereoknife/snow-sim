using System;
using System.Runtime.CompilerServices;
using HPML;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TFM.Components
{
    public class Terrain : MonoBehaviour
    {
        [SerializeField] public Texture2D heightmap;
        [SerializeField] public float sizeX;
        [SerializeField] public float sizeZ;
        [SerializeField] public float height;
        [SerializeField] public float units;
    }
}
