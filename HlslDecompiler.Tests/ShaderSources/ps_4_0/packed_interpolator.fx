cbuffer cb : register(b0)
{
	float4 scale;
	float3 sunDir;
	float fogStart;
	float fogEnd;
	float3 fogColour;
};

SamplerState samp;
Texture2D layer0;
Texture2D layer1;
Texture2D layer2;
Texture2D blendMap;

struct PS_IN
{
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = 0.800000012 * saturate(dot(normalize(i.normal), -sunDir)) + 0.200000003;
	float3 t1 = blendMap.Sample(samp, i.texcoord).xyz;
	float t2 = max(t1.y + t1.x + t1.z, 0.0000999999975);
	float3 t3 = t1 / t2;
	float3 t4 = layer1.Sample(samp, i.texcoord * scale.yy).xyz;
	float3 t5 = layer0.Sample(samp, i.texcoord * scale.xx).xyz;
	float3 t6 = layer2.Sample(samp, i.texcoord * scale.zz).xyz;
	float3 t7 = t5 * t3.x + t3.y * t4 + t6 * t3.z;
	return float4(saturate((i.texcoord1 - fogStart) / (fogEnd - fogStart)) * (-t7 * t0 + fogColour) + t0 * t7, 1);
}
