SamplerState samplerState0;
Texture2D texture0;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(frac(texcoord.z), frac(texcoord.z), texture0.Sample(samplerState0, texcoord.xy).ww);
}
