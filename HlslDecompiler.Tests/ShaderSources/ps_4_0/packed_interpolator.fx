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
	return float4(saturate((i.texcoord1 - fogStart) / (fogEnd - fogStart)) * (-(layer2.Sample(samp, i.texcoord * scale.zz).xyz * blendMap.Sample(samp, i.texcoord).z / max(blendMap.Sample(samp, i.texcoord).z + blendMap.Sample(samp, i.texcoord).y + blendMap.Sample(samp, i.texcoord).x, 0.0000999999975) + layer0.Sample(samp, i.texcoord * scale.xx).xyz * blendMap.Sample(samp, i.texcoord).x / max(blendMap.Sample(samp, i.texcoord).z + blendMap.Sample(samp, i.texcoord).y + blendMap.Sample(samp, i.texcoord).x, 0.0000999999975) + blendMap.Sample(samp, i.texcoord).y / max(blendMap.Sample(samp, i.texcoord).z + blendMap.Sample(samp, i.texcoord).y + blendMap.Sample(samp, i.texcoord).x, 0.0000999999975) * layer1.Sample(samp, i.texcoord * scale.yy).xyz) * (0.800000012 * saturate(dot(normalize(i.normal), -sunDir)) + 0.200000003) + fogColour) + (0.800000012 * saturate(dot(normalize(i.normal), -sunDir)) + 0.200000003) * (layer2.Sample(samp, i.texcoord * scale.zz).xyz * blendMap.Sample(samp, i.texcoord).z / max(blendMap.Sample(samp, i.texcoord).z + blendMap.Sample(samp, i.texcoord).y + blendMap.Sample(samp, i.texcoord).x, 0.0000999999975) + layer0.Sample(samp, i.texcoord * scale.xx).xyz * blendMap.Sample(samp, i.texcoord).x / max(blendMap.Sample(samp, i.texcoord).z + blendMap.Sample(samp, i.texcoord).y + blendMap.Sample(samp, i.texcoord).x, 0.0000999999975) + blendMap.Sample(samp, i.texcoord).y / max(blendMap.Sample(samp, i.texcoord).z + blendMap.Sample(samp, i.texcoord).y + blendMap.Sample(samp, i.texcoord).x, 0.0000999999975) * layer1.Sample(samp, i.texcoord * scale.yy).xyz), 1);
}
