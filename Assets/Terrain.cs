using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class Terrain : MonoBehaviour
{
    [SerializeField] private Texture2D heightmap;
    
    [SerializeField] private float height = 10f;

    public float Height => height;
    public int Xsize => heightmap.width;
    public int Zsize => heightmap.height;
    
    public NativeArray<ushort> GetHeightmapData() => heightmap.GetPixelData<ushort>(0);
}
