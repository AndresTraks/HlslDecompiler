float4 probe;

SamplerState samp;
TextureCubeArray probes;
Texture2D albedoMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalize(i.texcoord1);
	float3 t1 = albedoMap.Sample(samp, i.texcoord).xyz;
	float3 t2 = normalize(i.normal);
	float t3 = 1 - saturate(dot(t2, t0));
	float t4 = t3 * t3;
	float t5 = 0.959999979 * t3 * t4 * t4 + 0.0399999991;
	float t6 = 2 * dot(-t0, t2);
	float3 t7 = t2 * -t6 - t0;
	return float4(t5 * (probes.SampleLevel(samp, float4(t7, probe.x), albedoMap.Sample(samp, i.texcoord).w * probe.y).xyz - t1) + t1, 1);
}
