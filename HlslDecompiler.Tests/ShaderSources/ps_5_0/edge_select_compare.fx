cbuffer C : register(b0)
{
	float2 texel;
	float threshold;
};

SamplerState lin;
Texture2D src;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t1 = src.SampleLevel(lin, i.texcoord, 0, int2(-1, 0)).xyz;
	float3 t2 = src.SampleLevel(lin, i.texcoord, 0, int2(0, -1)).xyz;
	float3 t3 = src.SampleLevel(lin, i.texcoord, 0, int2(0, 1)).xyz;
	float3 t4 = src.SampleLevel(lin, i.texcoord, 0, int2(1, 0)).xyz;
	float3 t0 = src.SampleLevel(lin, i.texcoord, 0).xyz;
	float t5 = dot(t0, float3(0.298999995, 0.587000012, 0.114000000));
	float4 t6 = float4(dot(t2, float3(0.298999995, 0.587000012, 0.114000000)), dot(t3, float3(0.298999995, 0.587000012, 0.114000000)), dot(t4, float3(0.298999995, 0.587000012, 0.114000000)), dot(t1, float3(0.298999995, 0.587000012, 0.114000000)));
	float t7 = max(t5, max(max(t6.z, t6.w), max(t6.x, t6.y)));
	if (t7 - min(t5, min(min(t6.z, t6.w), min(t6.x, t6.y))) < t7 * threshold) return float4(t0, 1);
	bool t8 = abs(t6.x + t6.y - 2 * t5) >= abs(t6.z + t6.w - 2 * t5);
	float t9 = (t8 ? abs(t6.x - t5) : abs(t6.w - t5)) >= (t8 ? abs(t6.y - t5) : abs(t6.z - t5)) ? -0.5 : 0.5;
	float3 t10 = src.SampleLevel(lin, float2(t8 ? 0 : texel.x, t8 ? texel.y : 0) * t9 + i.texcoord, 0).xyz;
	return float4(lerp(t0, t10, 0.5), 1);
}
