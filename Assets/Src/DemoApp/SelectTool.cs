using System;
using DemoApp;
using HPML;
using TFM.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using static Unity.Mathematics.math;

public class SelectTool : MonoBehaviour
{
    [SerializeField] private SimulationController sim;
    [SerializeField] private Camera cam;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    private float k;
    //private NativeReference<int> intersection;

    private RenderParams rp;

    private void Awake()
    {
        material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        rp = new RenderParams(material);
    }

    private void Start()
    {
        k = (float)field.lipschitz(sim.Heightfield);
    }

    void LateUpdate()
    {
        var mousePos = Mouse.current.position;
        //var ray = HandleUtility.GUIPointToWorldRay(mousePos);
        var ray = cam.ScreenPointToRay(new(mousePos.x.value, mousePos.y.value, 0));
        
        //Debug.Log($"Origin: {ray.origin}, direction: {ray.direction}");

        var intersection = new NativeReference<int>(Allocator.TempJob);
        var job = new RaycastJob
        {
            heightfield = sim.Heightfield,
            snowfield = sim.Snowfield,
            origin = ray.origin,
            direction = ray.direction,
            k = k,
            intersection = intersection,
        };
        
        job.Schedule().Complete();

        if (intersection.Value < 0)
        {
            intersection.Dispose();
            return;
        }

        var normal = (float3)field.normal(sim.Heightfield, intersection.Value);
        var position = (float3)sim.Heightfield.coord(intersection.Value) / 100f;
        var rotation = Quaternion.LookRotation(-normal, Vector3.up);
        //rotation = Quaternion.identity;
        var scale = new Vector3(1, 1, 1);
        var tf = Matrix4x4.TRS(position, rotation, scale);
        
        Debug.Log((Vector3)position);

        Graphics.RenderMesh(rp, mesh, 0, tf);
        intersection.Dispose();
    }
    
    [BurstCompile]
    private struct RaycastJob : IJob
    {
        public doubleF heightfield;
        public double4F snowfield;
        public float3 origin, direction;
        public float k;
        public NativeReference<int> intersection;
        
        public void Execute()
        {
            var bmin = float3(0);
            var bmax = (float3)heightfield.size / 100f;
            var tmin = (bmin - origin) / direction;
            var tmax = (bmax - origin) / direction;
            var t0 = min(tmin, tmax);
            var t1 = max(tmin, tmax);
            var tnear = cmax(t0);
            var tfar = cmin(t1);
            
            tnear = max(0, tnear + 0.0001f);
            intersection.Value = -1;

            int i = 0;
            while (tnear < tfar)
            {
                var point = (origin + direction * tnear) * 100f;
                //point += float3((float2)sim.Heightfield.cellSize * 0.5f, 0).xzy;
                var ij = int2(point.xz * heightfield.iCellSize);
                var h = (float)heightfield[ij];
                h += (float)csum(snowfield[ij]);
                if (point.y < h + 0.001)
                {
                    intersection.Value = heightfield.index(ij);
                    break;
                }

                var dist = (float2)frac(point.xz * heightfield.iCellSize);
                var minStep = select(dist, 1 - dist, direction.xz < 0) / 100f / direction.xz;
                var d = (point.y - h) / k / 100f;
                tnear += max(d, max(cmin(minStep), 0.001f));
                i++;

                if (i > csum(heightfield.dimension)) break;
            }
        }
    }
}
