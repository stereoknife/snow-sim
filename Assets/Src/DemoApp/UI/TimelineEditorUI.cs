using System;
using ImGuiNET;
using UImGui;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace DemoApp
{
    public class TimelineEditorUI : MonoBehaviour
    {
        private bool showValues;

        private const int defaultYear = 2001;
        private DateTime _startDate = new (defaultYear, 11, 1);
        private DateTime _endDate = new (defaultYear + 1, 4, 30);
        
        private Calendar _precipCal, _cloudCal, _windCal;
        private HistCalendar _tempCal;

        private SimulationController _controller;
        private NativeList<float> _tempTimeline;
        private NativeList<float> _windTimeline;
        private NativeList<float> _cloudTimeline;
        private NativeList<float> _precipTimeline;

        private float _maxTemp = 20, _minTemp = -5;
        private float _meanWind, _windAmpl;
        private float _precipChance, _precipAmt;
        private float _avgCloud;

        private void Awake()
        {
            _controller = GetComponent<SimulationController>();
            _tempCal = new(_startDate, _endDate, 30, -20);
            _precipCal = new(_startDate, _endDate);
            _cloudCal = new(_startDate, _endDate);
            _windCal = new(_startDate, _endDate);
        }

        private void OnLayout(UImGui.UImGui obj)
        {
            var show = enabled;
            if (!ImGui.Begin("Timeline editor", ref show)) return;
            enabled = show;
            
            var sDay = _startDate.Day;
            var sMonth = _startDate.Month;
            var eDay = _endDate.Day;
            var eMonth = _endDate.Month;

            const int width = 112;
            bool sUpd = false, eUpd = false;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt("", ref sDay, 1, 7))
            {
                sDay %= DateTime.DaysInMonth(_startDate.Year, sMonth) + 1;
                if (sDay == 0) sDay++;
                _startDate = new(defaultYear, sMonth, sDay);
                if (_startDate > _endDate) _endDate = _endDate.AddYears(1);
                else if (_startDate < _endDate.AddYears(-1)) _endDate = _endDate.AddYears(-1);
                sUpd |= true;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt("Start date", ref sMonth, 1, 4))
            {
                sMonth %= 12 + 1;
                if (sMonth == 0) sMonth++;
                sDay = clamp(sDay, 1, DateTime.DaysInMonth(_startDate.Year, sMonth));
                _startDate = new(defaultYear, sMonth, sDay);
                if (_startDate > _endDate) _endDate = _endDate.AddYears(1);
                else if (_startDate < _endDate.AddYears(-1)) _endDate = _endDate.AddYears(-1);
                sUpd |= true;
            }

            var eYear = defaultYear;
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt("", ref eDay, 1, 7))
            {
                eDay %= DateTime.DaysInMonth(_endDate.Year, eMonth) + 1;
                if (eDay == 0) eDay++;
                _endDate = new(eYear, eMonth, eDay);
                if (_startDate > _endDate) _endDate = _endDate.AddYears(1);
                else if (_startDate < _endDate.AddYears(-1)) _endDate = _endDate.AddYears(-1);
                eUpd |= true;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(width);
            if (ImGui.InputInt("End date", ref eMonth, 1, 4))
            {
                eMonth %= 12 + 1;
                if (eMonth == 0) eMonth++;
                eDay = clamp(eDay, 1, DateTime.DaysInMonth(_endDate.Year, eMonth));
                _endDate = new(eYear, eMonth, eDay);
                if (_startDate > _endDate) _endDate = _endDate.AddYears(1);
                else if (_startDate < _endDate.AddYears(-1)) _endDate = _endDate.AddYears(-1);
                eUpd |= true;
            }

            if (sUpd) _precipCal.startDate = _tempCal.startDate = _cloudCal.startDate = _windCal.startDate = _startDate;
            if (eUpd) _precipCal.endDate = _tempCal.endDate = _cloudCal.endDate = _windCal.endDate = _endDate;
            
            ImGui.Text($"Simulation duration: {(_endDate - _startDate).Days} days");
            
            var save = ImGui.Button("Save this");
            ImGui.SameLine();
            ImGui.Button("Save all");
            ImGui.SameLine();
            ImGui.Checkbox("Show values", ref showValues);
            
            ImGui.NewLine();

            bool a = true;
            if (ImGui.BeginTabBar("MyTabBar"))
            {
                var n = ImGuiTabItemFlags.NoAssumedClosure & ImGuiTabItemFlags.NoCloseWithMiddleMouseButton;
                var u = ImGuiTabItemFlags.UnsavedDocument & n;
                
                if (ImGui.BeginTabItem("Precipitation"))
                {
                    ImGui.SliderFloat("Monthly precipitation chance", ref _precipChance, 0, 1);
                    ImGui.SliderFloat("Precipitation strength", ref _precipAmt, 0, 1);
                    if (ImGui.Button("Generate")) GeneratePrecip();
                    ImGui.NewLine();
                    _precipCal.AllMonths(14f);
                    ImGui.EndTabItem();
                    if (save) _precipCal.CopyToTimeline(_precipTimeline);
                }
                if (ImGui.BeginTabItem("Cloud cover"))
                {
                    if (ImGui.Button("Generate")) GenerateCloud();
                    ImGui.NewLine();
                    _cloudCal.AllMonths(14f);
                    ImGui.EndTabItem();
                    if (save) _cloudCal.CopyToTimeline(_cloudTimeline);
                }
                if (ImGui.BeginTabItem("Wind speed"))
                {
                    ImGui.SliderFloat("Mean wind speed", ref _meanWind, 0, 1);
                    ImGui.SliderFloat("Wind variation", ref _windAmpl, 0, 1);
                    ImGui.Button("Generate");
                    
                    ImGui.NewLine();
                    _windCal.AllMonths(14f);
                    ImGui.EndTabItem();
                    if (save) _windCal.CopyToTimeline(_windTimeline);
                }
                if (ImGui.BeginTabItem("Temperature"))
                {
                    ImGui.NewLine();
                    if (ImGui.SliderFloat("Maximum yearly temperature at base", ref _maxTemp, -10, 25))
                        _tempCal.MaxValue = _maxTemp;
                    if (ImGui.SliderFloat("Minimum yearly temperature at base", ref _minTemp, -10, 25))
                        _tempCal.MinValue = _minTemp;
                    if (ImGui.Button("Generate")) GenerateTemp();
                    
                    _tempCal.AllMonths();
                    ImGui.EndTabItem();
                    if (save) _tempCal.CopyToTimeline(_tempTimeline);
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        private void SaveTimelines()
        {
            _tempCal.CopyToTimeline(_tempTimeline);
            _cloudCal.CopyToTimeline(_cloudTimeline);
            _windCal.CopyToTimeline(_windTimeline);
            _precipCal.CopyToTimeline(_precipTimeline);
        }

        private void OnInitialize(UImGui.UImGui obj)
        {
            _tempTimeline = _controller.Simulation.TempTimeline;
            _cloudTimeline = _controller.Simulation.PrecipTimeline;
            _windTimeline = _controller.Simulation.WindTimeline;
            _precipTimeline = _controller.Simulation.PrecipTimeline;
        }

        private void OnDeinitialize(UImGui.UImGui obj)
        {
            // Runs after UImGui.OnDisable();
        }
        
        private void OnEnable()
        {
            UImGuiUtility.Layout += OnLayout;
            UImGuiUtility.OnInitialize += OnInitialize;
            UImGuiUtility.OnDeinitialize += OnDeinitialize;
        }

        private void OnDisable()
        {
            UImGuiUtility.Layout -= OnLayout;
            UImGuiUtility.OnInitialize -= OnInitialize;
            UImGuiUtility.OnDeinitialize -= OnDeinitialize;
        }

        private void GenerateTemp()
        {
            var array = _tempCal.Array;
            
            var a = (_maxTemp - _minTemp) / 2f;
            var b = PI2 / 365f;
            var c = 15f;
            var d = (_maxTemp + _minTemp) / 2f;
            
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = a * cos(b * (i - c) - PI) + d;
            }
        }

        private void GeneratePrecip()
        {
            var array = _precipCal.Array;
            var chance = unlerp(-PI/2, PI/2, asin(2 * _precipChance - 1));

            float daysOver0 = 0f;
            for (int i = 0; i < array.Length; i++)
            {
                var val = noise.cnoise(float2(0.5f, i * 0.3f)) * 0.5f + 0.5f - (1f - chance);
                array[i] = max(0, val);
                if (val >= 0) daysOver0++;
            }
            Debug.Log($"Precip days: {100 * daysOver0/array.Length}%");
        }
        
        private void GenerateCloud()
        {
            var array = _cloudCal.Array;
            float daysOver0 = 0f;
            for (int i = 0; i < array.Length; i++)
            {
                var val = noise.cnoise(float2(0.5f, i * 0.3f)) * 0.5f + 0.5f;
                array[i] = val;
                if (val >= 0.2) daysOver0++;
            }
            Debug.Log($"Cloud days: {100 * daysOver0/array.Length}%");
        }
    }
}