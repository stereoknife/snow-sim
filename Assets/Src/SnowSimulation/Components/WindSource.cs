using System;
using HPML;
using TFM.Simulation;
using TFM.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace TFM.Components
{
    public class WindSource : MonoBehaviour
    {
        [SerializeField][Range(0, 360)] private double windHeading;
        [SerializeField] private double windSpeedAtBase;

        [SerializeField] private bool updateSmoothWind = true, updateRoughWind;
        
        [SerializeField] private Material windMaterial;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Color smoothWindCol, roughWindcol;
        [SerializeField] private Color smoothTerrainCol, roughTerraincol;

        [SerializeField] private bool
            drawSmoothWind = true,
            drawRoughWind,
            drawSmoothTerrain,
            drawRoughTerrain = true;

        private doubleF _height;
        private doubleF _gaussianHeight;
        private double3F _wind;
        private doubleF _alt, _spd;

        private Mesh rtm, stm, rwm, swm;

        private Wind.WindEffectSurfaceJob wej;
        private RenderParams swrp, rwrp, strp, rtrp;
        private Transform t;

        private void Awake()
        {
            t = transform;
            rtm = new Mesh();
            rtm.name = "Rough Terrain";
            stm = new Mesh();
            stm.name = "Smooth Terrain";
            rwm = new Mesh();
            rwm.name = "Rough Wind";
            swm = new Mesh();
            swm.name = "Smooth Wind";
            
            var terrain = GetComponent<Terrain>();
            _height = doubleF.FromTexture(terrain.heightmap, new(terrain.sizeX, terrain.height, terrain.sizeZ), Allocator.Persistent);
            _wind = new double3F(_height, Allocator.Persistent, double3(cos(degrees(windHeading)), 0, sin(degrees(windHeading))) * windSpeedAtBase);
            _alt = new doubleF(_height, Allocator.Persistent);
            _spd = new doubleF(_height, Allocator.Persistent);
            _gaussianHeight = new doubleF(_height, Allocator.Persistent);

            var jh0 = new JobHandle();

            var jh1 = _height.GenerateMesh(out Mesh.MeshDataArray mda_rt, jh0);
            var jh2 = new Gaussian
            {
                gaussian = _gaussianHeight,
                height = _height,
                kernel = 15,
            }.ScheduleParallel(_height.Length, 64,default);
            jh2 = _gaussianHeight.GenerateMesh(out var mda_st, jh2);

            var wp = Wind.Parameters.Default;
            jh0 = Wind.VenturiParallel(_wind, _height, ref wp, jh0);
            jh0 = Wind.TerrainDeflectionParallel(_wind, _height,  ref wp, jh0);
            
            var init = new Wind.InitializeWESValuesJob
            {
                height = _height,
                wind = _wind,
                altitude = _alt,
                vspeed = _spd
            };
            jh0 = init.ScheduleParallel(_height.Length, 256, jh0);
            JobHandle.CombineDependencies(jh0, jh1, jh2).Complete();

            Mesh.ApplyAndDisposeWritableMeshData(mda_rt, rtm);
            rtm.RecalculateBounds();
            Mesh.ApplyAndDisposeWritableMeshData(mda_st, stm);
            stm.RecalculateBounds();
            stm.RecalculateNormals();

            swrp = new RenderParams(windMaterial);
            swrp.matProps = new MaterialPropertyBlock();
            swrp.matProps.SetColor("_BaseColor", smoothWindCol);
            rwrp = new RenderParams(windMaterial);
            rwrp.matProps = new MaterialPropertyBlock();
            rwrp.matProps.SetColor("_BaseColor", roughWindcol);
            
            strp = new RenderParams(terrainMaterial);
            strp.matProps = new MaterialPropertyBlock();
            strp.matProps.SetColor("_BaseColor", smoothTerrainCol);
            rtrp = new RenderParams(terrainMaterial);
            rtrp.matProps = new MaterialPropertyBlock();
            rtrp.matProps.SetColor("_BaseColor", roughTerraincol);
            
            wej = new Wind.WindEffectSurfaceJob
            {
                height = _height,
                gaussian = _gaussianHeight,
                wind = _wind,
                altitude = _alt,
                vspeed = _spd,
                falloff = 0.007,
                iteration = 0
            };
        }

        private int i = 0;
        
        private void Update()
        {
            Mesh.MeshDataArray rmda = default, smda = default;
            JobHandle sjh = default, rjh = default;
            
            if (updateSmoothWind)
            {
                wej.gaussian = _gaussianHeight;
                sjh = wej.ScheduleParallel(_wind.Length, 64, sjh);
                wej.iteration++;
                sjh = wej.ScheduleParallel(_wind.Length, 64, sjh);
                wej.iteration++;
                sjh = _alt.GenerateMesh(out smda, sjh);
            }
            
            if (updateRoughWind)
            {
                wej.gaussian = _height;
                rjh = wej.ScheduleParallel(_wind.Length, 64, rjh);
                wej.iteration++;
                rjh = wej.ScheduleParallel(_wind.Length, 64, rjh);
                wej.iteration++;
                rjh = _alt.GenerateMesh(out rmda, rjh);
            }
            
            JobHandle.CombineDependencies(sjh, rjh).Complete();

            if (updateSmoothWind)
            {
                Mesh.ApplyAndDisposeWritableMeshData(smda, swm);
                swm.RecalculateBounds();
            }
            if (updateRoughWind)
            {
                Mesh.ApplyAndDisposeWritableMeshData(rmda, rwm);
                rwm.RecalculateBounds();
            }
            
            if (drawSmoothWind)
                Graphics.RenderMesh(swrp, swm, 0, t.localToWorldMatrix);
            if (drawRoughWind)
                Graphics.RenderMesh(rwrp, rwm, 0, t.localToWorldMatrix);
            if (drawSmoothTerrain)
                Graphics.RenderMesh(strp, stm, 0, t.localToWorldMatrix);
            if (drawRoughTerrain)
                Graphics.RenderMesh(rtrp, rtm, 0, t.localToWorldMatrix);
        }

        [BurstCompile]
        private struct Gaussian : IJobFor
        {
            [ReadOnly] public doubleF height;
            public doubleF gaussian;
            public int kernel;
            
            public void Execute(int index)
            {
                double smooth = 0.005;
                int2 cell = height.cell(index);
                int2 start = clamp(cell - kernel, 0, height.dimension);
                int2 end = clamp(cell + kernel + 1, 0, height.dimension);
                double norm = 0, r = 0;
                
                for (int j = start.x; j < end.x; j++)
                {
                    for (int k = start.y; k < end.y; k++)
                    {
                        double2 d = double2(j, k) * height.cellSize;
                        double w = exp(-lengthsq(d) * smooth);
                        norm += w;
                        r += height[j, k] * w;
                    }
                }

                gaussian[index] = r / norm;
            }
        }

        private struct Deflection : IJobFor
        {
            [ReadOnly] public doubleF height;
            [NativeDisableContainerSafetyRestriction] public double3F wind;
            public int chunkSize;
            public int chunkIndex;
            public double2 direction;
            
            public void Execute(int index)
            {
                var realIndex = chunkIndex * chunkSize;
                var cell = wind.cell(realIndex);
                cell.y *= chunkSize;
                var y = cell.y + index / chunkSize;
                var x = cell.x + index % chunkSize;
                var i = wind.index(x, y);
                
                var w = wind[i];
                var len = length(w);
                var bw = direction * len;
                var normal = field.normal(height, i);
                var nxz = normal.xz;
                var cw = vec.cw(nxz);
                var ccw = vec.ccw(nxz);
                var nxzt = select(ccw, cw, dot(cw, bw) > 0);
                var nw = bw * (1 - length(nxz)) + 0.5 * len * nxzt;
                w.xz = nw;
                wind[x, y] = w;
            }
        }

        private void OnDestroy()
        {
            _height.Dispose();
            _spd.Dispose();
            _wind.Dispose();
            _alt.Dispose();
        }
    }
}
