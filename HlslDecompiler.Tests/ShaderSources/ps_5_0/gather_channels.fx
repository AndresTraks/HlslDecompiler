SamplerComparisonState cmp;
SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return tex.GatherGreen(samp, texcoord) + tex.GatherBlue(samp, texcoord) + tex.GatherAlpha(samp, texcoord) + tex.GatherCmpBlue(cmp, texcoord, 0.5);
}
