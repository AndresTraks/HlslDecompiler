SamplerState samp;
SamplerComparisonState sampcmp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex.SampleLevel(samp, texcoord.xy, 2, int2(1, -1));
	r1 = tex.Gather(samp, texcoord.xy, int2(-3, 4));
	r0 = r0 + r1;
	r1 = tex.Load(int3(1, 2, 0), int2(5, -6));
	r0 = r0 + r1;
	r1.x = tex.SampleCmpLevelZero(sampcmp, texcoord.xy, 0.5, int2(-7, 7));
	o = r0 + r1.x;

	return o;
}
