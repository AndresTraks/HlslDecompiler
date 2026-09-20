SamplerState samp;
Texture1DArray tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex.Sample(samp, texcoord.xy);
	r1 = tex.SampleLevel(samp, texcoord.zw, 2);
	o = r0 * r1;

	return o;
}
