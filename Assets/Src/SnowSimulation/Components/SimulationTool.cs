using System;
using HPML;
using TFM.Components.Visualization;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using Plane = Unity.Mathematics.Geometry.Plane;

using static Unity.Mathematics.math;

namespace TFM.Components
{
    [EditorTool("Simulation Tool", typeof(SimulationController))]
    public class SimulationTool : EditorTool, IDrawSelectedHandles
    {
        public override void OnWillBeDeactivated()
        {
            (target as SimulationController)?.HighlightPoint(-1);
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView)) return;
            if (Event.current.type != EventType.MouseMove && Event.current.type != EventType.MouseDrag && Event.current.type != EventType.MouseDown) return;
            
            var mousePos = Event.current.mousePosition;
            var ray = HandleUtility.GUIPointToWorldRay(mousePos);
            var sim = target as SimulationController;

            if (sim == null) return;

            var origin = (float3)ray.origin;
            var direction = (float3)ray.direction;

            var bmin = float3(0);
            var bmax = (float3)sim.Heightfield.size / 100f;
            var tmin = (bmin - origin) / direction;
            var tmax = (bmax - origin) / direction;
            var t0 = min(tmin, tmax);
            var t1 = max(tmin, tmax);
            var tnear = cmax(t0);
            var tfar = cmin(t1);
                
            tnear = max(0, tnear + 0.0001f);
            var inter = -1;

            var k = (float)field.lipschitz(sim.Heightfield);

            int i = 0;
            while (tnear < tfar)
            {
                var point = (float3)ray.GetPoint(tnear) * 100f;
                //point += float3((float2)sim.Heightfield.cellSize * 0.5f, 0).xzy;
                var ij = int2(point.xz * sim.Heightfield.iCellSize);
                var h = (float)sim.Heightfield[ij];
                h += (float)csum(sim.Snowfield[ij]);
                if (point.y < h + 0.001)
                {
                    inter = sim.Heightfield.index(ij);
                    break;
                }

                var dist = (float2)frac(point.xz * sim.Heightfield.iCellSize);
                var minStep = select(dist, 1 - dist, direction.xz < 0) / 100f / direction.xz;
                var d = (point.y - h) / k / 100f;
                tnear += max(d, max(cmin(minStep), 0.001f));
                i++;

                if (i == 2000)
                    Debug.Log($"i : {i}, t: {tnear}, d: {d}, inter: {inter}");
                if (i > 2000)
                {
                    Debug.Log($"i : {i}, t: {tnear}, d: {d}, inter: {inter}");
                    Debug.DrawLine(ray.GetPoint(0), ray.GetPoint(tnear), Color.red, 60f);
                    break;
                }
            }
            
            switch (Event.current.type)
            {
                case EventType.MouseDrag:
                    sim.SelectPoint(inter, false);
                    break;
                case EventType.MouseDown:
                    sim.SelectPoint(inter, true);
                    break;
                default:
                    sim.HighlightPoint(inter);
                    break;
            }
        }

        // This is draw gizmos for simulation controller
        public void OnDrawHandles()
        {
            
        }
    }
}