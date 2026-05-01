using System;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace TFM.Components
{
    public class Weather : MonoBehaviour
    {
        [SerializeField] private float2 temperatureRange;
        [SerializeField] private float temperatureChangeSpeed;
        [SerializeField] private float temperatureSeed;

        [SerializeField] private float windDirection;
        [SerializeField] private float windSpeed;
        
        [SerializeField] private Transform heightMarker;

        private float3 temp;
        private float3 speed;

        public float Temperature => lerp(temperatureRange.x, temperatureRange.y, noise.cnoise(mad(Time.time, temp.xy, temp.z)));
        public float Altitude0C => Temperature * 0.1f;

        private void Awake()
        {
            temp = speed = new float3();
            temp.xy = new float2(0, 1);
            temp.z = temperatureSeed;
        }

        private void Update()
        {
            var pos = heightMarker.localPosition;
            pos.y = Altitude0C;
            heightMarker.localPosition = pos;
        }
    }
}