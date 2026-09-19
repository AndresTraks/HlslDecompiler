float time;
float3 eyePos;
float4 tint;

SamplerState samp;
Texture2D normal0;
Texture2D normal1;
Texture2D reflection;
Texture2D refraction;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = 2 * normal0.Sample(samp, float2(0.0199999996 * time + i.texcoord2.x, i.texcoord2.y)).xyz - 1 + 2 * normal1.Sample(samp, float2(1.70000005 * i.texcoord2.x, 1.70000005 * i.texcoord2.y - 0.0130000003 * time)).xyz - 1;
	float t1 = length(t0);
	float2 t2 = t0.xz / t1;
	float t3 = 1 - saturate(dot(normalize(eyePos - i.texcoord), float3(t2.x, t0.y / t1, t2.y)));
	float t4 = t3 * t3;
	float t5 = 0.899999976 * t4 * t4 * t3 + 0.100000001;
	float2 t6 = 0.5 * (i.texcoord1.xy / i.texcoord1.w) + 0.5;
	float2 t7 = 0.0199999996 * -t2 + t6;
	float3 t8 = refraction.Sample(samp, t7).xyz;
	return float4((t5 * (reflection.Sample(samp, 0.0299999993 * t2 + t6).xyz - t8) + t8) * tint.xyz, 1);
}
