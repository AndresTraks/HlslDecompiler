SamplerState samp;
SamplerComparisonState sampcmp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float t0 = tex.SampleCmpLevelZero(sampcmp, texcoord.xy, 0.5, int2(-7, 7)).x;
	float4 t1 = tex.Gather(samp, texcoord.xy, int2(-3, 4));
	float4 t2 = tex.SampleLevel(samp, texcoord.xy, 2, int2(1, -1));
	return t2 + t1 + tex.Load(int3(1, 2, 0), int2(5, -6)) + t0;
}
