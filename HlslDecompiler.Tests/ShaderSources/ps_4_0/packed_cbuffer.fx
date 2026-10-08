cbuffer cb : register(b0)
{
	float time;
	float3 eyePos;
	float4 tint;
};

SamplerState samp;
Texture2D normal0;
Texture2D normal1;
Texture2D reflection;
Texture2D refraction;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : SV_Target
{
	float2 t0 = 0.5 * (i.texcoord1.xy / i.texcoord1.w) + 0.5;
	float3 t1 = normal0.Sample(samp, float2(0.0199999996 * time + i.texcoord2.x, i.texcoord2.y)).xyz;
	float3 t2 = normalize(eyePos - i.texcoord);
	float2 t3 = float2(1.70000005 * i.texcoord2.x, 1.70000005 * i.texcoord2.y - 0.0130000003 * time);
	float3 t4 = normal1.Sample(samp, t3).xyz;
	float3 t5 = 2 * t1 - 1 + 2 * t4 - 1;
	float3 t6 = normalize(t5);
	float2 t7 = t6.xz;
	float t8 = 1 - saturate(dot(t2, float3(t7.x, t6.y, t7.y)));
	float t9 = t8 * t8;
	float t10 = 0.899999976 * t9 * t9 * t8 + 0.100000001;
	float3 t11 = refraction.Sample(samp, -0.0199999996 * t7 + t0).xyz;
	float3 t12 = reflection.Sample(samp, 0.0299999993 * t7 + t0).xyz;
	return float4(lerp(t11, t12, t10) * tint.xyz, 1);
}
