#ifndef TERRAIN_SHADER_INCLUDED
#define TERRAIN_SHADER_INCLUDED

StructuredBuffer<float> Snow;

void GetSnow_float(in int Id, out float Value)
{
    Value = Snow[Id];
}

#endif
