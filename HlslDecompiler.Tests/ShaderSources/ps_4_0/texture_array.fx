SamplerState samp;
Texture2DArray tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.Sample(samp, texcoord.xyz);
}
