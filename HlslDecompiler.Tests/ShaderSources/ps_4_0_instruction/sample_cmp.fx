SamplerComparisonState cmpSamp;
Texture2D shadowMap;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = shadowMap.x.SampleCmpLevelZero(cmpSamp, texcoord.x, texcoord.z);
	o = r0.x;

	return o;
}
