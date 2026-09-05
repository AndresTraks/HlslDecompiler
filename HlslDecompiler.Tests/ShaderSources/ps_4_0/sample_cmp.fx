SamplerComparisonState cmpSamp;
Texture2D shadowMap;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return shadowMap.SampleCmpLevelZero(cmpSamp, texcoord.xy, texcoord.z).x;
}
