using System;
using HPML;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TFM.Components
{
    [RequireComponent(typeof(SimulationController))]
    public class TerrainRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField] private Color terrain, compacted, stable, unstable, powder;
        [SerializeField] private float4 renderBox = new (0f, 1f, 0f, 1f);
        [SerializeField] private float scale = 1000;
    
        private doubleF _heightfield;
        private double4F _snowfield;

        private BatchRendererGroup m_BRG;

        private GraphicsBuffer m_InstanceData;
        private BatchID m_BatchID;
        private BatchMeshID m_MeshID;
        private BatchMaterialID m_MaterialID;

        private NativeArray<float3x4> _objectToWorld;
        private NativeArray<float3x4> _worldToObject;
        private NativeArray<float4> _colors;
    
        private uint _byteAddressObjectToWorld;
        private uint _byteAddressWorldToObject;
        private uint _byteAddressColor;

        // Some helper constants to make calculations more convenient.
        private const int kSizeOfMatrix = sizeof(float) * 4 * 4;
        private const int kSizeOfPackedMatrix = sizeof(float) * 4 * 3;
        private const int kSizeOfFloat4 = sizeof(float) * 4;
        private const int kBytesPerInstance = (kSizeOfPackedMatrix * 2) + kSizeOfFloat4;
        private const int kExtraBytes = kSizeOfMatrix * 2;
        private const int kSizeRow = 505;
        private const int kSizeLayer = kSizeRow * kSizeRow;
        private const int kNumInstances = kSizeLayer * 2;
        private const float kSizeBox = 8.5f;

        private JobHandle _jobHandle = default;

        // The PackedMatrix is a convenience type that converts matrices into
        // the format that Unity-provided SRP shaders expect.

        private void Start()
        {
            var sim = GetComponent<SimulationController>();
            _heightfield = sim.height;
            _snowfield = sim.snow;

            _objectToWorld = new NativeArray<float3x4>(kNumInstances, Allocator.Persistent);
            _worldToObject = new NativeArray<float3x4>(kNumInstances, Allocator.Persistent);
            
            m_BRG = new BatchRendererGroup(this.OnPerformCulling, IntPtr.Zero);
            m_MeshID = m_BRG.RegisterMesh(mesh);
            m_MaterialID = m_BRG.RegisterMaterial(material);

            AllocateInstanceDateBuffer();
            PopulateInstanceDataBuffer();
        }

        private void AllocateInstanceDateBuffer()
        {
            m_InstanceData = new GraphicsBuffer(GraphicsBuffer.Target.Raw, GraphicsBuffer.UsageFlags.LockBufferForWrite,
                BufferCountForInstances(kBytesPerInstance, kNumInstances, kExtraBytes),
                sizeof(int));
        }

        private void PopulateInstanceDataBuffer()
        {
            var zero = new float4x4[1] { float4x4.zero };

            var ctmj = new InitMatrices
            {
                heightfield = _heightfield,
                objectToWorld = _objectToWorld,
                worldToObject = _worldToObject,
                scale = 1/scale
            };
            
            var jh = ctmj.Schedule(kSizeLayer, default);
            
            _colors = new NativeArray<float4>(kNumInstances, Allocator.Persistent);
            for (int i = 0; i < kNumInstances; i++)
            {
                _colors[i] = math.float4(0);
            }
            
            _byteAddressObjectToWorld = kSizeOfPackedMatrix * 2;
            _byteAddressWorldToObject = _byteAddressObjectToWorld + kSizeOfPackedMatrix * kNumInstances;
            _byteAddressColor = _byteAddressWorldToObject + kSizeOfPackedMatrix * kNumInstances;
            
            jh.Complete();
            
            m_InstanceData.SetData(zero, 0, 0, 1);
            m_InstanceData.SetData(_objectToWorld, 0, (int)(_byteAddressObjectToWorld / kSizeOfPackedMatrix), _objectToWorld.Length);
            m_InstanceData.SetData(_worldToObject, 0, (int)(_byteAddressWorldToObject / kSizeOfPackedMatrix), _worldToObject.Length);
            m_InstanceData.SetData(_colors, 0, (int)(_byteAddressColor / kSizeOfFloat4), _colors.Length);
            
            var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
            metadata[0] = new MetadataValue { NameID = Shader.PropertyToID("unity_ObjectToWorld"), Value = 0x80000000 | _byteAddressObjectToWorld, };
            metadata[1] = new MetadataValue { NameID = Shader.PropertyToID("unity_WorldToObject"), Value = 0x80000000 | _byteAddressWorldToObject, };
            metadata[2] = new MetadataValue { NameID = Shader.PropertyToID("_Levels"), Value = 0x80000000 | _byteAddressColor, };
            
            m_BatchID = m_BRG.AddBatch(metadata, m_InstanceData.bufferHandle);
        }
        
        int BufferCountForInstances(int bytesPerInstance, int numInstances, int extraBytes = 0)
        {
            // Round byte counts to int multiples
            bytesPerInstance = (bytesPerInstance + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            extraBytes = (extraBytes + sizeof(int) - 1) / sizeof(int) * sizeof(int);
            int totalBytes = bytesPerInstance * numInstances + extraBytes;
            return totalBytes / sizeof(int);
        }
    
        private void OnDestroy()
        { 
            m_BRG.Dispose();
            m_InstanceData.Dispose();
            _worldToObject.Dispose();
            _objectToWorld.Dispose();
        }

        private void Update()
        {
            var csj = new CopySnowLevels
            {
                levels = _colors.GetSubArray(kSizeLayer, kSizeLayer),
                snowfield = _snowfield
            };
            
            var csh = csj.ScheduleParallel(kSizeLayer, 256, default);
            
            var cmj = new ComputeMatrices
            {
                heightfield = _heightfield,
                snowfield = _snowfield,
                objectToWorld = _objectToWorld,
                worldToObject = _worldToObject,
                scale = 1f / 1000f,
            };
            var cmh = cmj.ScheduleParallel(kSizeLayer, 256, default);
            
            JobHandle.CombineDependencies(cmh, csh).Complete();
            
            m_InstanceData.SetData(_objectToWorld, kSizeLayer, (int)(_byteAddressObjectToWorld / kSizeOfPackedMatrix) + kSizeLayer, kSizeLayer);
            m_InstanceData.SetData(_worldToObject, kSizeLayer, (int)(_byteAddressWorldToObject / kSizeOfPackedMatrix) + kSizeLayer, kSizeLayer);
            m_InstanceData.SetData(_colors, kSizeLayer, (int)(_byteAddressColor / kSizeOfFloat4) + kSizeLayer, kSizeLayer);
        }

        private unsafe JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {

            int alignment = UnsafeUtility.AlignOf<long>();
            
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            
            drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
            drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int), alignment, Allocator.TempJob);
            drawCommands->drawCommandPickingEntityIds = null;

            drawCommands->drawCommandCount = 1;
            drawCommands->drawRangeCount = 1;
            drawCommands->visibleInstanceCount = kNumInstances;
            
            drawCommands->instanceSortingPositions = null;
            drawCommands->instanceSortingPositionFloatCount = 0;
            
            drawCommands->drawCommands[0].visibleOffset = 0;
            drawCommands->drawCommands[0].visibleCount = kSizeLayer;
            drawCommands->drawCommands[0].batchID = m_BatchID;
            drawCommands->drawCommands[0].materialID = m_MaterialID;
            drawCommands->drawCommands[0].meshID = m_MeshID;
            drawCommands->drawCommands[0].submeshIndex = 0;
            drawCommands->drawCommands[0].splitVisibilityMask = 0xff;
            drawCommands->drawCommands[0].flags = 0;
            drawCommands->drawCommands[0].sortingPosition = 0;
            
            drawCommands->drawRanges[0].drawCommandsType = BatchDrawCommandType.Direct;
            drawCommands->drawRanges[0].drawCommandsBegin = 0;
            drawCommands->drawRanges[0].drawCommandsCount = 1;
            
            drawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };
            
            drawCommands->drawCommands[0].visibleCount = 0;
            var job = new CullEmptySnow
            {
                snowfield = _snowfield,
                heightfield = _heightfield,
                visibleCount = &drawCommands->drawCommands[0].visibleCount,
                visibleInstances = drawCommands->visibleInstances,
                renderBox = renderBox
            };
            var jh = job.Schedule(kSizeLayer, default);
            return jh;
        }

        [BurstCompile]
        private struct InitMatrices : IJobFor
        {
            [ReadOnly] public doubleF heightfield;
            [NativeDisableContainerSafetyRestriction] public NativeArray<float3x4> objectToWorld, worldToObject;
            public float scale;
            
            public void Execute(int index)
            {
                var ij = heightfield.cell(index);
                var h = heightfield[index] * 0.5;
                var c = heightfield.cellSize;
                var t = (float3)math.double3(c * 0.5f + c * ij, h).xzy * scale;
                var s = (float3)math.double3(heightfield.cellSize, h * 2).xzy * scale;
                var m = float4x4.TRS(t, quaternion.identity, s);
                objectToWorld[index] = m.xyz();
                worldToObject[index] = math.inverse(m).xyz();
                
                t.y += (float)(h + 2 * c.x) * scale;
                s.y = (float)(c.x * 4) * scale;
                m = float4x4.TRS(t, quaternion.identity, s);
                objectToWorld[index + kSizeLayer] = m.xyz();
                worldToObject[index + kSizeLayer] = math.inverse(m).xyz();
            }
        }

        [BurstCompile]
        private struct ComputeMatrices : IJobFor
        {
            [ReadOnly] public doubleF heightfield;
            [ReadOnly] public double4F snowfield;
            public float scale;
            [NativeDisableContainerSafetyRestriction] public NativeArray<float3x4> objectToWorld, worldToObject;

        
            public void Execute(int index)
            {
                var ij = heightfield.cell(index);
                var h = heightfield[index];
                var c = heightfield.cellSize;
                var l = math.csum(snowfield[index]);
                
                var t = (float3)math.double3(c * 0.5f + c * ij, h + l * 0.5).xzy * scale;
                var s = (float3)math.double3(heightfield.cellSize, l).xzy * scale;
                objectToWorld[index + kSizeLayer] = Matrix(t, s);
                worldToObject[index + kSizeLayer] = Matrix(-t / s, 1 / s);
            }

            private float3x4 Matrix(float3 t, float3 s)
            {
                return math.float3x4(
                    s.x, 0, 0, t.x,
                    0, s.y, 0, t.y,
                    0, 0, s.z, t.z
                );
            }
        }
        
        [BurstCompile]
        private struct CopySnowLevels : IJobFor
        {
            [ReadOnly] public double4F snowfield;
            public NativeArray<float4> levels;

        
            public void Execute(int index)
            {
                levels[index] = (float4)snowfield[index];
            }
        }

        [BurstCompile]
        private unsafe struct CullEmptySnow : IJobFor
        {
            [ReadOnly] public double4F snowfield;
            [ReadOnly] public doubleF heightfield;
            [NativeDisableUnsafePtrRestriction] public int* visibleInstances;
            [NativeDisableUnsafePtrRestriction] public uint* visibleCount;

            public float4 renderBox;
            
            public void Execute(int index)
            {
                var c = heightfield.cell(index);
                if (math.any(c < renderBox.xz * heightfield.dimension | c > renderBox.yw * heightfield.dimension)) return;
                var j = *visibleCount;
                visibleInstances[j++] = index;
                if (math.any(snowfield[index] > 0)) visibleInstances[j++] = index + kSizeLayer;
                *visibleCount = j;
            }
        }
    }
}