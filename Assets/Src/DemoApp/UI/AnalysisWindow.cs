using System.Globalization;
using ImGuiNET;
using TFM.Components.Analysis;
using UImGui;
using UnityEngine;

namespace DemoApp
{
    public class AnalysisWindow : MonoBehaviour
    {
        private SimulationProfiler _profiler;
        private SimulationController _controller;

        public bool Show = false;

        private void Awake()
        {
            _controller = GetComponent<SimulationController>();
            //_profiler = _data.simulation.Profiler;
        }

        private void OnEnable()
        {
            UImGuiUtility.Layout += OnLayout;
            UImGuiUtility.OnInitialize += OnInitialize;
            UImGuiUtility.OnDeinitialize += OnDeinitialize;
            _profiler = _controller.Simulation.Profiler;
        }
        
        private void OnLayout(UImGui.UImGui obj)
        {
            if (!Show || !ImGui.Begin("Profiler", ref Show))
            {
                if (!_controller.IsRunning)
                {
                    ImGui.BeginTable("Average times", 4, ImGuiTableFlags.Borders);
                    ImGui.TableSetupColumn("Event");
                    ImGui.TableSetupColumn("Count");
                    ImGui.TableSetupColumn("Average time");
                    ImGui.TableSetupColumn("Total time");
                    
                    foreach (var (ev, record) in _profiler.Averages())
                    {
                        ImGui.TableNextRow();
                        ImGui.Text(ev.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(record.numEvents.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(record.averageDuration.ToString(CultureInfo.InvariantCulture));
                        ImGui.TableNextColumn();
                        ImGui.Text(record.totalDuration.ToString(CultureInfo.InvariantCulture));
                    }
                    
                    ImGui.EndTable();

                    if (ImGui.CollapsingHeader("Full event data"))
                    {
                        ImGui.BeginTable("Average times", 4, ImGuiTableFlags.Borders);
                        ImGui.TableSetupColumn("Order");
                        ImGui.TableSetupColumn("Event");
                        ImGui.TableSetupColumn("Start time");
                        ImGui.TableSetupColumn("Duration");

                        foreach (var record in _profiler.AllRecords())
                        {
                            ImGui.TableNextRow();
                            ImGui.Text(record.ord.ToString());
                            ImGui.TableNextColumn();
                            ImGui.Text(record.eventId.ToString());
                            ImGui.TableNextColumn();
                            ImGui.Text(record.startTime.ToString(CultureInfo.InvariantCulture));
                            ImGui.TableNextColumn();
                            ImGui.Text(record.duration.ToString(CultureInfo.InvariantCulture));
                        }
                        
                        ImGui.EndTable();
                    }
                }
                else
                {
                    ImGui.Text("Can't analyze while simulation is running.");
                }
            }
            ImGui.End();
        }

        private void OnInitialize(UImGui.UImGui obj)
        {
            // Runs after UImGui.OnEnable();
        }

        private void OnDeinitialize(UImGui.UImGui obj)
        {
            // Runs after UImGui.OnDisable();
        }

        private void OnDisable()
        {
            UImGuiUtility.Layout -= OnLayout;
            UImGuiUtility.OnInitialize -= OnInitialize;
            UImGuiUtility.OnDeinitialize -= OnDeinitialize;
        }
    }
}