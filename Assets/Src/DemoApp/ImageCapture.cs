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
    public class ImageCapture : EditorSimulationController
    {
        [SerializeField] private float[] fixedValues;
        [SerializeField] private float[] propTemp;
        [SerializeField] private float[] normalDist;
        [SerializeField] private float[] normalDistBaseline;
        [SerializeField] private float[] simple;

        IEnumerator Record()
        {
            new FileInfo($"{Application.persistentDataPath}/results/fixed/").Directory?.Create();
            _simulation.UseSimpleMelt = false;
            
            /*
            for (float i = 0f; i <= 0.002f; i += 0.0002f)
            {
                _simulation.Parameters.MeltTestFunction = 0;
                _simulation.Parameters.MeltConstTemp = -i;
                while(runSim && _simulation.simulationTime < time)
                {
                    _simulation.Step();
                    yield return null;
                }
                
                ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/results/fixed/{i}.png");
                yield return null;
                Reset();
            }
            */
            
            /*
            new FileInfo($"{Application.persistentDataPath}/results/prop/").Directory?.Create();
            foreach (var val in propTemp)
            {
                _simulation.Parameters.MeltTestFunction = 1;
                _simulation.Parameters.MeltVolumeFactor = val;
                while(runSim && _simulation.simulationTime < time)
                {
                    _simulation.Step();
                    yield return null;
                }
                
                ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/results/prop/{val}.png");
                yield return null;
                Reset();
            }
            */
            
            new FileInfo($"{Application.persistentDataPath}/results/norm/").Directory?.Create();
            foreach (var b in normalDist)
            {
                for (int j = 2; j < 9; j+=2)
                {
                    foreach (var a in normalDistBaseline)
                    {
                        for (int i = 2; i < 9; i+=2)
                        {
                            _simulation.Parameters.MeltTestFunction = 2;
                            _simulation.Parameters.MeltExpB = j * b;
                            _simulation.Parameters.MeltExpA = i * a;
                    
                            while(runSim && _simulation.simulationTime < time)
                            {
                                _simulation.Step();
                                yield return null;
                            }
                    
                            ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/results/norm/{j*b}-{i*a}.png");
                            yield return null;
                            Reset();
                        }
                    }
                }
            }
            
            /*
            _simulation.UseSimpleMelt = true;
            new FileInfo($"{Application.persistentDataPath}/results/simple/").Directory?.Create();
            for (int i = 0; i <= 50; i += i < 5 ? 1 : 5)
            {
                _simulation.Parameters.MeltVolumeFactor = i;
                while(runSim && _simulation.simulationTime < time)
                {
                    _simulation.Step();
                    yield return null;
                }
                
                ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/results/simple/{i}.png");
                yield return null;
                Reset();
            }
            */ 
            
            /*
            _simulation.UseSimpleMelt = true;
            new FileInfo($"{Application.persistentDataPath}/results/base/").Directory?.Create();
            _simulation.Parameters.MeltVolumeFactor = 0;
            var screenshot = 100f;
            while(runSim && _simulation.simulationTime < time)
            {
                _simulation.Step();
                yield return null;

                if (_simulation.simulationTime > screenshot)
                {
                    ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/results/base/{(int)_simulation.simulationTime}.png");
                    screenshot += 5;
                }
            }
            */
                
            ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/results/base/180.png");
            yield return null;
            Reset();
            
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }

        private void Update()
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                runSim = true;
                StartCoroutine(Record());
            }
        }
    }
}