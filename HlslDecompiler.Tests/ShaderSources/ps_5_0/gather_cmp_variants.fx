SamplerState samp;
SamplerComparisonState csamp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = tex.GatherCmp(csamp, texcoord, 0.5, int2(3, 4));
	float4 t1 = tex.Gather(samp, texcoord, int2(1, 2));
	float4 t2 = tex.GatherCmp(csamp, texcoord, 0.5);
	return t2 + t1 + t0;
}
