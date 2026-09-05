SamplerState samp;
Texture2DArray tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o = tex.Sample(samp, texcoord.xyz);

	return o;
}
