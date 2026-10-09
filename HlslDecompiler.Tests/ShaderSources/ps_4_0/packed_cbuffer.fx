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
	float3 t2 = normal1.Sample(samp, float2(1.70000005 * i.texcoord2.x, 1.70000005 * i.texcoord2.y - 0.0130000003 * time)).xyz;
	float3 t3 = 2 * t1 - 1 + 2 * t2 - 1;
	float3 t4 = normalize(t3);
	float2 t5 = t4.xz;
	float3 t6 = normalize(eyePos - i.texcoord);
	float t7 = 1 - saturate(dot(t6, float3(t5.x, t4.y, t5.y)));
	float t8 = t7 * t7;
	float t9 = 0.899999976 * t8 * t8 * t7 + 0.100000001;
	float3 t10 = reflection.Sample(samp, 0.0299999993 * t5 + t0).xyz;
	float3 t11 = refraction.Sample(samp, -0.0199999996 * t5 + t0).xyz;
	return float4(lerp(t11, t10, t9) * tint.xyz, 1);
}
