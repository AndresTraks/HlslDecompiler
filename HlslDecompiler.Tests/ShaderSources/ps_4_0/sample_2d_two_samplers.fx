SamplerState samplerState0;
SamplerState samplerState1;
Texture2D texture0;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = texture0.Sample(samplerState1, texcoord.yx).xy;
	return texture0.Sample(samplerState0, 2 * t0 + texcoord.yx).wzyx;
}
