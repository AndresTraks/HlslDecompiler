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
	float4 t4 = albedoMap.Sample(samp, i.texcoord);
	float3 t5 = probes.SampleLevel(samp, float4(t3, probe.x), t4.w * probe.y).xyz;
	return float4(lerp(t4.xyz, t5, t2), 1);
}
