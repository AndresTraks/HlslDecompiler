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
	float t0 = length(eyePos - i.texcoord);
	float3 t1 = 2 * normal1.Sample(samp, float2(1.70000005 * i.texcoord2.x, 1.70000005 * i.texcoord2.y - 0.0130000003 * time)).xyz + 2 * normal0.Sample(samp, float2(0.0199999996 * time + i.texcoord2.x, i.texcoord2.y)).xyz - 1 - 1;
	float t2 = length(t1);
	float2 t3 = t1.xz / t2;
	float t4 = 1 - saturate(dot((eyePos - i.texcoord) / t0, float3(t3.x, t1.y / t2, t3.y)));
	float t5 = t4 * t4;
	float t6 = 0.899999976 * t5 * t5 * t4 + 0.100000001;
	float2 t7 = 0.5 * i.texcoord1.xy / i.texcoord1.w + 0.5;
	float2 t8 = 0.0299999993 * t3 + t7;
	float2 t9 = 0.0199999996 * -t3 + t7;
	float3 t10 = refraction.Sample(samp, t9).xyz;
	return float4((t6 * (reflection.Sample(samp, t8).xyz - t10) + t10) * tint.xyz, 1);
}
