Texture2D texture0;
SamplerState samplerState0;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return texture0.Sample(samplerState0, 2 * texcoord.yx).wzyx;
}
