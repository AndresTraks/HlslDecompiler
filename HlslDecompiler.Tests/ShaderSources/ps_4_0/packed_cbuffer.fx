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
	float t1 = length(eyePos - i.texcoord);
	float3 t7 = 2 * normal1.Sample(samp, float2(1.70000005 * i.texcoord2.x, 1.70000005 * i.texcoord2.y - 0.0130000003 * time)).xyz + 2 * normal0.Sample(samp, float2(0.0199999996 * time + i.texcoord2.x, i.texcoord2.y)).xyz - 1 - 1;
	float t8 = length(t7);
	float2 t5 = t7.xz / t8;
	float t2 = 1 - saturate(dot((eyePos - i.texcoord) / t1, float3(t5.x, t7.y / t8, t5.y)));
	float t0 = t2 * t2;
	float t3 = 0.899999976 * t0 * t0 * t2 + 0.100000001;
	float2 t4 = 0.0299999993 * t5 + 0.5 * i.texcoord1.xy / i.texcoord1.w + 0.5;
	float2 t6 = 0.0199999996 * -t5 + 0.5 * i.texcoord1.xy / i.texcoord1.w + 0.5;
	return float4((t3 * (reflection.Sample(samp, t4).xyz - refraction.Sample(samp, t6).xyz) + refraction.Sample(samp, t6).xyz) * tint.xyz, 1);
}
