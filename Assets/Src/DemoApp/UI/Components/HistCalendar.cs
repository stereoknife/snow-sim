using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Unity.Collections;
using UnityEngine;

namespace DemoApp
{
    public class HistCalendar
    {
        private const int kdayWidth = 30;
        private const int kdayHeight = 20;

        public static float Width => kdayWidth * 7 + ImGui.GetStyle().ItemSpacing.x * 6;

        public DateTime startDate;
        public DateTime endDate;
        public float MaxValue;
        public float MinValue;
        
        public float[] values;
        
        public int MonthRange => 1 + endDate.Month + (endDate.Year - startDate.Year) * 12 - startDate.Month;
        public int DayRange => (endDate - startDate).Days;

        private int hash;
        
        public float[] Array => values;
        
        public IEnumerable<float> Values => values.Concat(values).Skip(startDate.DayOfYear - 1).Take((endDate - startDate).Days + 1);


        public bool Unsaved => hash != values.GetHashCode();

        public HistCalendar(DateTime startDate, DateTime endDate, float maxValue, float minValue)
        {
            this.startDate = startDate;
            this.endDate = endDate;
            values = new float[365];
            MaxValue = maxValue;
            MinValue = minValue;
            hash = values.GetHashCode();
        }
        
        public unsafe void CopyToTimeline(NativeList<float> tl)
        {
            tl.Clear();
            tl.Capacity = DayRange;
            Debug.Log($"Capacity {tl.Capacity}");
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
            Debug.Log($"Length: {tl.Length}");

            hash = values.GetHashCode();
        }
        
        public void Month(int month)
        {
            ImGui.BeginGroup();

            var day = startDate.AddDays(1 - startDate.Day).AddMonths(month);
            var firstDayAsIndex = day.DayOfYear - 1;
            ImGui.PushID(day.Month);
            ImGui.Text(day.ToString("MMM"));
            for (int i = 0; i < DateTime.DaysInMonth(day.Year, day.Month); i++)
            {
                if (i != 0) ImGui.SameLine();
                ImGui.VSliderFloat($"##{i}", new Vector2(10, 100), ref values[firstDayAsIndex + i], MinValue, MaxValue, "");
                ImGui.SetItemTooltip($"{values[firstDayAsIndex + i]:f2}°C");
            }
            ImGui.PopID();
            ImGui.EndGroup();
        }

        public void AllMonths()
        {
            for (int i = 0; i <= MonthRange; i++)
            {
                Month(i);
            }
        }
    }
}