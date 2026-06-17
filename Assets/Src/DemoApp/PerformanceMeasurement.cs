using System;
using System.Collections;
using System.IO;
using System.Linq;
using EasyButtons;
using HPML;
using HPML.Serialization;
using TFM.Components;
using TFM.Components.Visualization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Serialization.Binary;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemoApp
{
    public class PerformanceMeasurement : EditorSimulationController
    {

        IEnumerator Record()
        {
            var start = DateTime.Now;
            while(runSim && _simulation.simulationTime < time)
            {
                _simulation.Step();
                yield return null;
            }
            var end = DateTime.Now;
            
            Debug.Log($"Duration: {(end - start).TotalSeconds} seconds");
            
            var file = $"{Application.persistentDataPath}/results/profiling.csv";
            _simulation.Profiler.WriteToFile(file, false, true);
            
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }

        private void Start()
        {
            runSim = true;
            StartCoroutine(Record());
        }
    }
}