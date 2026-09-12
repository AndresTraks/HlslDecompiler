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
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(saturate((i.texcoord.z - fogStart) / (fogEnd - fogStart)) * (-(layer2.Sample(samp, i.texcoord.xy * scale.zz).xyz * blendMap.Sample(samp, i.texcoord.xy).z / max(blendMap.Sample(samp, i.texcoord.xy).z + blendMap.Sample(samp, i.texcoord.xy).y + blendMap.Sample(samp, i.texcoord.xy).x, 0.0000999999975) + layer0.Sample(samp, i.texcoord.xy * scale.xx).xyz * blendMap.Sample(samp, i.texcoord.xy).x / max(blendMap.Sample(samp, i.texcoord.xy).z + blendMap.Sample(samp, i.texcoord.xy).y + blendMap.Sample(samp, i.texcoord.xy).x, 0.0000999999975) + blendMap.Sample(samp, i.texcoord.xy).y / max(blendMap.Sample(samp, i.texcoord.xy).z + blendMap.Sample(samp, i.texcoord.xy).y + blendMap.Sample(samp, i.texcoord.xy).x, 0.0000999999975) * layer1.Sample(samp, i.texcoord.xy * scale.yy).xyz) * (0.800000012 * saturate(dot(normalize(i.normal), -sunDir)) + 0.200000003) + fogColour) + (0.800000012 * saturate(dot(normalize(i.normal), -sunDir)) + 0.200000003) * (layer2.Sample(samp, i.texcoord.xy * scale.zz).xyz * blendMap.Sample(samp, i.texcoord.xy).z / max(blendMap.Sample(samp, i.texcoord.xy).z + blendMap.Sample(samp, i.texcoord.xy).y + blendMap.Sample(samp, i.texcoord.xy).x, 0.0000999999975) + layer0.Sample(samp, i.texcoord.xy * scale.xx).xyz * blendMap.Sample(samp, i.texcoord.xy).x / max(blendMap.Sample(samp, i.texcoord.xy).z + blendMap.Sample(samp, i.texcoord.xy).y + blendMap.Sample(samp, i.texcoord.xy).x, 0.0000999999975) + blendMap.Sample(samp, i.texcoord.xy).y / max(blendMap.Sample(samp, i.texcoord.xy).z + blendMap.Sample(samp, i.texcoord.xy).y + blendMap.Sample(samp, i.texcoord.xy).x, 0.0000999999975) * layer1.Sample(samp, i.texcoord.xy * scale.yy).xyz), 1);
}
