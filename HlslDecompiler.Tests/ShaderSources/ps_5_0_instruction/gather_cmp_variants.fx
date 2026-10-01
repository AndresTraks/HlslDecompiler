SamplerState samp;
SamplerComparisonState csamp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex.GatherCmp(csamp, texcoord.xy, 0.5);
	r1 = tex.Gather(samp, texcoord.xy, int2(1, 2));
	r0 = r0 + r1;
	r1 = tex.GatherCmp(csamp, texcoord.xy, 0.5, int2(3, 4));
	o = r0 + r1;

	return o;
}
