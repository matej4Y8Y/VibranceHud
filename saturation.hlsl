Texture2D<float4> desktopTex : register(t0);
SamplerState linearSampler : register(s0);

cbuffer ColorMatrix : register(b0)
{
    float4 row0;
    float4 row1;
    float4 row2;
    float4 row3;
    float4 row4;
};

struct PSInput
{
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

float4 main(PSInput input) : SV_Target
{
    float4 c = desktopTex.Sample(linearSampler, input.uv);

    // Apply the 5x5 color matrix (row-major, identical to ColorAdjust.Build()).
    // The bottom row is identity for alpha and the homogeneous w coordinate.
    float4 outColor;
    outColor.r = dot(row0, float4(c.rgb, 1.0));
    outColor.g = dot(row1, float4(c.rgb, 1.0));
    outColor.b = dot(row2, float4(c.rgb, 1.0));
    outColor.a = c.a;
    return outColor;
}
