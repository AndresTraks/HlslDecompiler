SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float t0 = tex.CalculateLevelOfDetail(samp, texcoord.xy);
	float4 t1 = tex.SampleLevel(samp, texcoord.xy, t0);
	float t2 = tex.CalculateLevelOfDetailUnclamped(samp, texcoord.xy);
	return (t2 - t0) * t1;
}
