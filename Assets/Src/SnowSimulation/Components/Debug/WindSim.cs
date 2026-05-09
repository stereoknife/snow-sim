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
    public class WindSim : MonoBehaviour
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
        private doubleF _alts, _spds, _altr, _spdr;

        private Mesh rtm, stm, rwm, swm;

        private Wind.WindEffectSurfaceJob wej;
        private RenderParams swrp, rwrp, strp, rtrp;
        private Transform t;
        private FieldRenderer _fr;

        private void Awake()
        {
            _fr = GetComponent<FieldRenderer>();
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
            var scale = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * terrain.units;
            _height = doubleF.FromTexture(terrain.heightmap, scale, Allocator.Persistent);
            var heading = double3(cos(radians(windHeading)), 0, sin(radians(windHeading)));
            Debug.Log(heading);
            _wind = new double3F(_height, Allocator.Persistent, heading * windSpeedAtBase);
            _alts = new doubleF(_height, Allocator.Persistent);
            _spds = new doubleF(_height, Allocator.Persistent);
            _altr = new doubleF(_height, Allocator.Persistent);
            _spdr = new doubleF(_height, Allocator.Persistent);
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
            jh0 = _fr.RegisterField(_wind, FieldRenderer.Name.DeflectedWind, jh0);
            
            var init = new Wind.InitializeWESValuesJob
            {
                height = _height,
                gaussian = _height,
                wind = _wind,
                altitude = _alts
            };
            //var jh3 = init.ScheduleParallel(_height.Length, 256, jh0);
            init.gaussian = _gaussianHeight;
            var jh4 = init.ScheduleParallel(_height.Length, 256, JobHandle.CombineDependencies(jh0, jh2));
            JobHandle.CombineDependencies(jh1, jh4).Complete();

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
                falloff = Wind.Parameters.Default.SurfaceFalloff,
                iteration = 0,
                altitude = _alts
            };
        }

        private int i = 0;
        
        private void Update()
        {
            Mesh.MeshDataArray rmda = default, smda = default;
            JobHandle sjh = default, rjh = default;
            updateRoughWind = false;
            
            if (updateSmoothWind)
            {
                wej.gaussian = _gaussianHeight;
                sjh = wej.ScheduleParallel(_wind.Length, 64, sjh);
                wej.iteration++;
                sjh = wej.ScheduleParallel(_wind.Length, 64, sjh);
                wej.iteration++;
                var jh0 = _alts.GenerateMesh(out smda, sjh);
                var jh1 = _fr.RegisterField(_alts, FieldRenderer.Name.Heightmap, sjh);
                sjh = JobHandle.CombineDependencies(jh0, jh1);
            }
            
            if (updateRoughWind)
            {
                wej.gaussian = _height;
                rjh = wej.ScheduleParallel(_wind.Length, 64, rjh);
                wej.iteration++;
                rjh = wej.ScheduleParallel(_wind.Length, 64, rjh);
                wej.iteration++;
                var jh0 = _altr.GenerateMesh(out rmda, rjh);
                var jh1 = _fr.RegisterField(_altr, FieldRenderer.Name.WindAltitude, rjh);
                rjh = JobHandle.CombineDependencies(jh0, jh1);
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
            
            if (wej.iteration > 1000) Debug.Break();
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
                        double2 d = (double2(j, k) - cell) * height.cellSize;
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
            _spds.Dispose();
            _spdr.Dispose();
            _wind.Dispose();
            _alts.Dispose();
            _altr.Dispose();
            _gaussianHeight.Dispose();
        }
    }
}
