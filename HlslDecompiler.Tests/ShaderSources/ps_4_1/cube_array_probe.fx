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
	float t0 = 1 - saturate(dot(normalize(i.normal), normalize(i.texcoord1)));
	float t1 = t0 * t0;
	float t2 = 0.959999979 * t0 * t1 * t1 + 0.0399999991;
	float3 t3 = reflect(-normalize(i.texcoord1), normalize(i.normal));
	return float4(lerp(albedoMap.Sample(samp, i.texcoord).xyz, probes.SampleLevel(samp, float4(t3, probe.x), albedoMap.Sample(samp, i.texcoord).w * probe.y).xyz, t2), 1);
}
