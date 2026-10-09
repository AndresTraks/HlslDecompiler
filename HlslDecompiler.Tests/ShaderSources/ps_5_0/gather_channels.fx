SamplerComparisonState cmp;
SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = tex.GatherAlpha(samp, texcoord);
	float4 t1 = tex.GatherBlue(samp, texcoord);
	float4 t2 = tex.GatherCmpBlue(cmp, texcoord, 0.5);
	float4 t3 = tex.GatherGreen(samp, texcoord);
	return t3 + t1 + t0 + t2;
}
