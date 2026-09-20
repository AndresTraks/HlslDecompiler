SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	float4 r1;
	r0.x = tex.CalculateLevelOfDetailUnclamped(samp, texcoord.xy);
	r0.y = tex.CalculateLevelOfDetail(samp, texcoord.xy);
	r0.x = -(r0.y) + r0.x;
	r1 = tex.SampleLevel(samp, texcoord.xy, r0.y);
	o = r0.x * r1;

	return o;
}
