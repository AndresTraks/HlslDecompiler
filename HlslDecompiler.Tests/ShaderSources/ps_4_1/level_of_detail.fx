SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float t0 = tex.CalculateLevelOfDetail(samp, texcoord.xy);
	float t1 = tex.CalculateLevelOfDetailUnclamped(samp, texcoord.xy);
	float4 t2 = tex.SampleLevel(samp, texcoord.xy, t0);
	return (t1 - t0) * t2;
}
