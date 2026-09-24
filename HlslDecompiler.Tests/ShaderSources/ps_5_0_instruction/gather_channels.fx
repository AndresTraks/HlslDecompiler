SamplerComparisonState cmp;
SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex.GatherGreen(samp, texcoord.xy);
	r1 = tex.GatherBlue(samp, texcoord.xy);
	r0 = r0 + r1;
	r1 = tex.GatherAlpha(samp, texcoord.xy);
	r0 = r0 + r1;
	r1 = tex.GatherCmpBlue(cmp, texcoord.xy, 0.5);
	o = r0 + r1;

	return o;
}
