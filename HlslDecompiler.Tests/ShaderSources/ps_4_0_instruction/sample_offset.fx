SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o = tex.Sample(samp, texcoord.xy);

	return o;
}
