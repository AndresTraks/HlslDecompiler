sampler2D baseMap;
float4 blendControl;
sampler2D blendMap : register(s2);
sampler2D detailMap;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float3 t0 = tex2D(baseMap, texcoord).xyz;
	float t1 = tex2D(blendMap, texcoord).x;
	return float4(blendControl.y - t1 >= 0 ? t0 : lerp(t0, 2 * tex2D(detailMap, texcoord * blendControl.x).xyz * t0, t1), tex2D(baseMap, texcoord).w);
}
