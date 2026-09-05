SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.SampleLevel(samp, texcoord.xy, 2);
}
