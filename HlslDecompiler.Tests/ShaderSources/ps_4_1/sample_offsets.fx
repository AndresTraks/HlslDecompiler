SamplerState samp;
SamplerComparisonState sampcmp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.SampleLevel(samp, texcoord.xy, 2, int2(1, -1)) + tex.Gather(samp, texcoord.xy, int2(-3, 4)) + tex.Load(int3(1, 2, 0), int2(5, -6)) + tex.SampleCmpLevelZero(sampcmp, texcoord.xy, 0.5, int2(-7, 7)).x;
}
