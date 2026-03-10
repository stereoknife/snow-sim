using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainRenderer : MonoBehaviour
{
    [SerializeField] private Material material, snow;
    [SerializeField] private Mesh cube;
    private Terrain terrain;
    private NativeArray<Matrix4x4> instanceData;

    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        instanceData = new NativeArray<Matrix4x4>(terrain.Xsize * terrain.Zsize, Allocator.Persistent);
    }

    private void Update()
    {
        var job = new InstanceTransformJob
        {
            heightmap = terrain.GetHeightmapData(),
            height = terrain.Height,
            instanceData = instanceData,
            resolution = terrain.Xsize,
        };

        var handle = job.ScheduleParallel(instanceData.Length, 32, new JobHandle());
        handle.Complete();
        
        var rp = new RenderParams(material);
        Graphics.RenderMeshInstanced(rp, cube, 0, instanceData);
        
        var snowjob = new SnowInstanceTransformJob
        {
            heightmap = terrain.GetHeightmapData(),
            height = terrain.Height,
            instanceData = instanceData,
            resolution = terrain.Xsize,
        };

        handle = job.ScheduleParallel(instanceData.Length, 32, handle);
        handle.Complete();
        
        rp = new RenderParams(material);
        Graphics.RenderMeshInstanced(rp, cube, 0, instanceData);
    }
    
    private struct InstanceTransformJob : IJobFor
    {
        public NativeArray<Matrix4x4> instanceData;
        [ReadOnly] public NativeArray<ushort> heightmap;
        public int resolution;
        public float height;
    
        public void Execute(int index)
        {
            var i = index / resolution;
            var j = index % resolution;
            var h = ((float)heightmap[index]/(float)ushort.MaxValue) * height;
            instanceData[index] = Matrix4x4.TRS(
                new Vector3(i, h * 0.5f, j),
                Quaternion.identity,
                new Vector3(1, h, 1)
            );
        }
    }
    
    private struct SnowInstanceTransformJob : IJobFor
    {
        public NativeArray<Matrix4x4> instanceData;
        [ReadOnly] public NativeArray<ushort> heightmap;
        [ReadOnly] public NativeArray<ushort> baseHeightmap;
        public int resolution;
        public float height;
    
        public void Execute(int index)
        {
            var i = index / resolution;
            var j = index % resolution;
            var h = ((float)heightmap[index]/(float)ushort.MaxValue) * height;
            instanceData[index] = Matrix4x4.TRS(
                new Vector3(i, h * 0.5f + baseHeightmap[index], j),
                Quaternion.identity,
                new Vector3(1, h, 1)
            );
        }
    }
}