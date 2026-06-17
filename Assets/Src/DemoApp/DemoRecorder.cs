#if UNITY_EDITOR
using System.Collections;
using EasyButtons;
using TFM.Components;
using TFM.Components.Visualization;
using TFM.Solvers;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.Recorder;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemoApp
{
    public class DemoRecorder : EditorSimulationController
    {
        [SerializeField] private Preset settings;
        [SerializeField] private string recordingName;
        [SerializeField] private float[] values;

        [Button]
        private void StartRecording()
        {
            StartCoroutine(Record());
        }

        IEnumerator Record()
        {
            var recorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            settings.ApplyTo(recorderSettings);
            recorderSettings.FrameRate = 30;
            recorderSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            
            for (int i = 0; i < values.Length; i++)
            {
                Debug.Log(recorderSettings.FileNameGenerator.FileName);
                recorderSettings.FileNameGenerator.FileName = $"{recordingName}_{values[i]}";
                var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                controllerSettings.AddRecorderSettings(recorderSettings);
                controllerSettings.SetRecordModeToManual();
                controllerSettings.ExitPlayMode = true;
                var recorderController = new RecorderController(controllerSettings);
                var simulationParameters = _simulation.Parameters;
                simulationParameters.MeltVolumeFactor = values[i];
                _simulation.Parameters = simulationParameters;
                _simulation.Enabled[StochasticSimulation.EventId.SnowfallStart] = true;
                recorderController.PrepareRecording();
                recorderController.StartRecording();
                while (_simulation.simulationTime < time - 1)
                {
                    _simulation.Step();
                    yield return null;
                    if (_simulation.simulationTime > minDay)
                    {
                        _simulation.Enabled[StochasticSimulation.EventId.SnowfallStart] = false;
                    }
                }
                recorderController.StopRecording();
                Reset();
                yield return null;
            }
            
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif