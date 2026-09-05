SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.SampleGrad(samp, texcoord.xy, ddx(texcoord.xy), ddy(texcoord.xy));
}
