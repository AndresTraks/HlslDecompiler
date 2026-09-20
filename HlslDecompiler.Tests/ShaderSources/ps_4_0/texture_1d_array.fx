SamplerState samp;
Texture1DArray tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.Sample(samp, texcoord.xy) * tex.SampleLevel(samp, texcoord.zw, 2);
}
