SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o = tex.SampleBias(samp, texcoord.xyxx, texcoord.z);

	return o;
}
