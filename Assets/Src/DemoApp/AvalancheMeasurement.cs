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
    public class AvalancheMeasurement : SimulationController
    {
        [SerializeField] private int measurements;
        [SerializeField] private bool resetAfterAvalanche;

        private doubleF snowDiff, snowSnapshot;

#if !UNITY_EDITOR
        private TerrainMeshRenderer _renderer;

        private void Start()
        {
            _renderer = GetComponent<TerrainMeshRenderer>();
            StartRecording();
        }
        
        private void Update()
        {
            if (Keyboard.current.tKey.wasReleasedThisFrame)
            {
                _renderer.enabled = !_renderer.enabled;
            }
        }
#endif
        
        [Button]
        private void StartRecording()
        {
            snowDiff = new doubleF(Heightfield, Allocator.Persistent);
            snowSnapshot = new doubleF(Heightfield, Allocator.Persistent);
            _simulation.SetSnow(initialSnowValue);
            StartCoroutine(Record());
        }

        IEnumerator Record()
        {
            for (int i = 0; i < measurements; i++)
            {
                Debug.Log($"Recording {i}");
                
                for (int j = 0; j < snowSnapshot.Length; j++)
                {
                    snowSnapshot[j] = math.csum(Snowfield[j]);
                }

                for (;;)
                {
                    _simulation.Step();
                    yield return null;
                    if (!_simulation.Moving.Any(x => x)) break;
                }
                
                for (int j = 0; j < snowSnapshot.Length; j++)
                {
                    snowDiff[j] += math.csum(Snowfield[j]) - snowSnapshot[j];
                }
                
                if (resetAfterAvalanche) _simulation.SetSnow(initialSnowValue);
            }
            
            WriteToDisk();
            CreateTexture();

            snowDiff.Dispose();
            snowSnapshot.Dispose();
            
            Debug.Log("Recording finished");
        }

        private unsafe void WriteToDisk()
        {
            using var buffer = new UnsafeAppendBuffer(doubleFAdapter.SizeOf(snowDiff), 4, Allocator.Temp);
            BinarySerialization.ToBinary(&buffer, snowDiff);
            var path = $"{Application.dataPath}/results/SnowDiff.bytes";
            new FileInfo(path).Directory?.Create();
            using var writer = new BinaryWriter(File.Open(path, FileMode.Create));
            writer.Write(new ReadOnlySpan<byte>(buffer.Ptr, buffer.Length));
        }

        private void CreateTexture()
        {
            var positiveDiff = new doubleF(snowDiff, Allocator.TempJob);
            var negativeDiff = new doubleF(snowDiff, Allocator.TempJob);
            for (int i = 0; i < snowDiff.Length; i++)
            {
                if (snowDiff[i] < 0) Debug.Log(snowDiff[i]);
                if (snowDiff[i] > 0) positiveDiff[i] = snowDiff[i];
                else negativeDiff[i] = snowDiff[i];
            }
            var positiveTex = new Texture2D(snowDiff.dimension.x, snowDiff.dimension.y, TextureFormat.RGBA32, false);
            var negativeTex = new Texture2D(snowDiff.dimension.x, snowDiff.dimension.y, TextureFormat.RGBA32, false);
            var pjh = positiveDiff.ToTexture2D(positiveTex, default);
            var njh = negativeDiff.ToTexture2D(negativeTex, default);
            JobHandle.CombineDependencies(pjh, njh).Complete();
            positiveTex.Apply(false, false);
            negativeTex.Apply(false,false);
            File.WriteAllBytes($"{Application.dataPath}/results/positiveDiff.png", positiveTex.EncodeToPNG());
            File.WriteAllBytes($"{Application.dataPath}/results/negativeDiff.png", negativeTex.EncodeToPNG());
            positiveDiff.Dispose();
            negativeDiff.Dispose();
        }
    }
}