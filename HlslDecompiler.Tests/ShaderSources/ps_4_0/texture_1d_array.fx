SamplerState samp;
Texture1DArray tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = tex.Sample(samp, texcoord.xy);
	float4 t1 = tex.SampleLevel(samp, texcoord.zw, 2);
	return t0 * t1;
}
