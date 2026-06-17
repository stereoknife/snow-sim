using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DemoApp
{
    public class Calendar
    {
        private const int kdayWidth = 30;
        private const int kdayHeight = 20;

        public static float Width => kdayWidth * 7 + ImGui.GetStyle().ItemSpacing.x * 6;

        public DateTime startDate;
        public DateTime endDate;
        public int MonthRange => 1 + endDate.Month + (endDate.Year - startDate.Year) * 12 - startDate.Month;
        public int DayRange => (endDate - startDate).Days + 1;
        public bool Unsaved => values.GetHashCode() != hash;

        private int hash;
        private float[] values;
        private bool[] hoverEnter;

        public float[] Array => values;

        public IEnumerable<float> Values => values.Concat(values).Skip(startDate.DayOfYear - 1).Take((endDate - startDate).Days + 1);

        public Calendar(DateTime startDate, DateTime endDate)
        {
            this.startDate = startDate;
            this.endDate = endDate;
            values = new float[365];
            hoverEnter = new bool[365];
            hash = values.GetHashCode();
        }

        public unsafe void CopyToTimeline(NativeList<float> tl)
        {
            tl.Capacity = DayRange;
            fixed (float* ptr = values)
            {
                var start = startDate.DayOfYear - 1;
                var end = endDate.DayOfYear;
                if (start > end)
                {
                    tl.AddRangeNoResize(&ptr[start], values.Length - start);
                    tl.AddRangeNoResize(&ptr, end);
                }
                else
                {
                    tl.AddRangeNoResize(&ptr[start], DayRange);
                }
            }

            hash = values.GetHashCode();
        }

        public void AllMonths(float spacing, bool printValues = false, bool fillPadding = false,
            DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            var hMonths = (int)(ImGui.GetContentRegionAvail().x / (Width + spacing / 2));
            hMonths = Mathf.Max(1, hMonths);
            
            for (int i = 0; i < MonthRange; i++)
            {
                if (i % hMonths != 0)
                {
                    ImGui.SameLine();
                    ImGui.Dummy(new (spacing, spacing));
                    ImGui.SameLine();
                }
                Month(i);
            }
        }
        
        public void Month(int month, bool printValues = false, bool fillPadding = false, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            ImGui.BeginGroup();
            ImGui.PushID(month);
            var day = startDate.AddDays(1 - startDate.Day).AddMonths(month);
            var firstDayAsIndex = day.DayOfYear - 1;
            ImGui.Text(day.ToString("MMM"));
            var dayOfWeek = day.DayOfWeek;
            while (day.DayOfWeek != firstDayOfWeek) day = day.AddDays(-1);

            var diff = firstDayOfWeek - day.DayOfWeek;

            string label;
            ImGui.BeginDisabled();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f,0f));
            for (; day.DayOfWeek != dayOfWeek; day = day.AddDays(1))
            {
                if (day.DayOfWeek != firstDayOfWeek) ImGui.SameLine();
                label = fillPadding ? $"{day.Day}" : "";
                ImGui.Button($"{label}###-{day.Day}", new Vector2(kdayWidth, kdayHeight));
            }
            ImGui.PopStyleColor(1);
            ImGui.EndDisabled();

            month = day.Month;
            var year = day.Year;
            
            for (; day.Month == month; day = day.AddDays(1))
            {
                var styles = 1;
                if (day.DayOfWeek != DayOfWeek.Monday) ImGui.SameLine();
                var i = day.DayOfYear - 1;
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f, values[i]));
                if (printValues && values[i] > 0.499f)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 0f, 0f, 1f));
                    styles++;
                }
                label = printValues ? $"{values[i] * 100f}%" : $"{day.Day}";
                if (ImGui.Button($"{label}###{day.Day}", new Vector2(30, 20)))
                {
                    /*
                    values[i] = Mathf.Clamp01(values[i] + 0.1f);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) values[i] = Mathf.Clamp01(values[i] + 0.1f);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) values[i] = Mathf.Clamp01(values[i] - 0.1f);
                    */
                }
                
                
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
                {
                    if (!hoverEnter[i])
                    {
                        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left)) values[i] = Mathf.Clamp01(values[i] + 0.1f);
                        if (ImGui.IsMouseDragging(ImGuiMouseButton.Right)) values[i] = Mathf.Clamp01(values[i] - 0.1f);
                    }
                    else
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) values[i] = Mathf.Clamp01(values[i] + 0.1f);
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) values[i] = Mathf.Clamp01(values[i] - 0.1f);
                    }
                    hoverEnter[i] = true;
                }
                else
                {
                    hoverEnter[i] = false;
                }
                
                
                ImGui.PopStyleColor(styles);
            }
            ImGui.PlotHistogram("##hist", ref values[firstDayAsIndex], DateTime.DaysInMonth(year, month), 0, "", 0, 1, new Vector2(Width, 19));
            ImGui.PopID();
            ImGui.EndGroup();
        }
    }
}