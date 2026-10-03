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
	float3 t1 = normalize(i.normal);
	float t2 = 1 - saturate(dot(t1, t0));
	float t3 = t2 * t2;
	float t4 = 0.959999979 * t2 * t3 * t3 + 0.0399999991;
	float3 t5 = reflect(-t0, t1);
	float4 t6 = albedoMap.Sample(samp, i.texcoord);
	float3 t7 = probes.SampleLevel(samp, float4(t5, probe.x), t6.w * probe.y).xyz;
	return float4(lerp(t6.xyz, t7, t4), 1);
}
