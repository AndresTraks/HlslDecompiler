SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.SampleBias(samp, texcoord.xy, texcoord.z);
}
