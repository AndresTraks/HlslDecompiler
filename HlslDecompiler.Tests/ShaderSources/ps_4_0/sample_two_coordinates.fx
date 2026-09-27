SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return float4(tex.Sample(samp, texcoord).y, tex.Sample(samp, 2 * texcoord).yz, 1);
}
