#ifndef TERRAIN_SHADER_INCLUDED
#define TERRAIN_SHADER_INCLUDED

float2 scale_xz;

float csum(float4 x)
{
    return x.x + x.y + x.z + x.w;
}

int csum(int4 x)
{
    return x.x + x.y + x.z + x.w;
}

float4 accsum(float4 v)
{
    float x = v.x;
    float y = x + v.y;
    float z = y + v.z;
    float w = z + v.w;
    return float4(x, y, z, w);
}

void vert_float(float3 In_position, float4 In_levels, out float3 Out_position)
{
    Out_position = In_position;
    Out_position += 0.5;
    Out_position.xz *= 1;
    Out_position.y *= csum(In_levels);
}

void frag_float(float3 In_position, float4 In_levels, float4 c0, float4 c1, float4 c2, float4 c3, float4 fallback, out float4 Out_color)
{
    float4 colors[5] = { c3, c2, c1, c0, fallback };
    float4 levels = accsum(In_levels);
    float height = (In_position.y + 0.5) * levels.w;
    int4 steps = step((float4)height, levels);
    int sum = csum(steps) - sign(levels.w);
    Out_color = colors[sum];
}

#endif
