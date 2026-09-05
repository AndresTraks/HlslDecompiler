SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return tex.Sample(samp, texcoord, int2(1, -1));
}
