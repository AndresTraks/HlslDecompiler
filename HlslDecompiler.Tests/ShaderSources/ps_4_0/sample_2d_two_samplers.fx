SamplerState samplerState0;
SamplerState samplerState1;
Texture2D texture0;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return texture0.Sample(samplerState0, 2 * texture0.Sample(samplerState1, texcoord.yx).xy + texcoord.yx).wzyx;
}
