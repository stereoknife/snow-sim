using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class TerrainGen : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] private Texture2D heightmap;
    [SerializeField] private Mesh cube;
    
    [SerializeField] private float pixelSize;
    [SerializeField] private float height = 10f;

    //private Matrix4x4[] instData;
    private NativeArray<Matrix4x4> instanceData;

    void Awake()
    {
        instanceData = new NativeArray<Matrix4x4>(heightmap.width * heightmap.height, Allocator.Persistent);
        //instData = new Matrix4x4[heightmap.width * heightmap.height];
        /*
        for (int i = 0; i < heightmap.width; i++)
        {
            for (int j = 0; j < heightmap.height; j++)
            {
                var h = heightmap.GetPixel(i, j).r * height;
                instData[i * heightmap.height + j] = Matrix4x4.TRS(
                    new Vector3(i, h * 0.5f, j),
                    Quaternion.identity,
                    new Vector3(1, h, 1)
                );
            }
        }
        */
    }

    private void Update()
    {
        var job = new InstanceTransformJob
        {
            heightmap = heightmap.GetPixelData<ushort>(0),
            height = height,
            instanceData = instanceData,
            resolution = heightmap.width,
        };

        var handle = job.ScheduleParallel(instanceData.Length, 32, new JobHandle());
        handle.Complete();
        
        var rp = new RenderParams(material);
        Graphics.RenderMeshInstanced(rp, cube, 0, instanceData);
    }
}
