float4 scale;
float3 sunDir;
float fogStart;
float fogEnd;
float3 fogColour;

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
	float3 t3 = layer0.Sample(samp, i.texcoord * scale.xx).xyz * (t1.x / t2) + t1.y / t2 * layer1.Sample(samp, i.texcoord * scale.yy).xyz + layer2.Sample(samp, i.texcoord * scale.zz).xyz * (t1.z / t2);
	return float4(saturate((i.texcoord1 - fogStart) / (fogEnd - fogStart)) * (-t3 * t0 + fogColour) + t0 * t3, 1);
}
