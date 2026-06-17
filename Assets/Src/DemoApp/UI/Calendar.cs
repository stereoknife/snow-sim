using System;
using ImGuiNET;
using UnityEngine;

namespace DemoApp.UI
{
    public class Calendar
    {
        private const int kdayWidth = 30;
        private const int kdayHeight = 20;

        public static float Width => kdayWidth * 7 + ImGui.GetStyle().ItemSpacing.x * 6;

        public DateTime startDate;
        public DateTime endDate;
        
        public float[] values;
        private bool[] hoverEnter;

        public Calendar(DateTime startDate, DateTime endDate)
        {
            this.startDate = startDate;
            this.endDate = endDate;
            var len = (endDate - startDate).Days + 1;
            values = new float[len];
            hoverEnter = new bool[len];
        }
        
        public void Month(int month, bool printValues = false, bool fillPadding = false, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            ImGui.BeginGroup();
            
            var day = startDate.AddDays(1 - startDate.Day).AddMonths(month);
            var firstDayAsIndex = (day - startDate).Days;
            ImGui.Text(day.ToString("MMM"));
            var dayOfWeek = day.DayOfWeek;
            while (day.DayOfWeek != firstDayOfWeek) day = day.AddDays(-1);

            var diff = firstDayOfWeek - day.DayOfWeek;

            string label;
            ImGui.BeginDisabled();
            for (; day.DayOfWeek != dayOfWeek; day = day.AddDays(1))
            {
                if (day.DayOfWeek != firstDayOfWeek) ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f,0f));
                label = fillPadding ? $"{day.Day}" : "";
                ImGui.Button($"{label}###m{month}d-{day.Day}", new Vector2(kdayWidth, kdayHeight));
            }
            ImGui.EndDisabled();

            month = day.Month;
            var year = day.Year;
            
            for (; day.Month == month; day = day.AddDays(1))
            {
                var styles = 1;
                if (day.DayOfWeek != DayOfWeek.Monday) ImGui.SameLine();
                var i = (day - startDate).Days;
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f, values[i]));
                if (printValues && values[i] > 0.499f)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 0f, 0f, 1f));
                    styles++;
                }
                label = printValues ? $"{values[i] * 100f}%" : $"{day.Day}";
                if (ImGui.Button($"{label}###m{month}d{day.Day}", new Vector2(30, 20)))
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
            ImGui.PushID(month);
            ImGui.PlotHistogram("###hist", ref values[firstDayAsIndex], DateTime.DaysInMonth(year, month), 0, "", 0, 1, new Vector2(Width, 19));
            ImGui.PopID();
            ImGui.EndGroup();
        }
    }
}