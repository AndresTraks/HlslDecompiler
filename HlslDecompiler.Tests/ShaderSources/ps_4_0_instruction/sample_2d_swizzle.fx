SamplerState samplerState0;
Texture2D texture0;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0.xy = texcoord.yx + texcoord.yx;
	r0 = texture0.Sample(samplerState0, r0.xy);
	o = r0.wzyx;

	return o;
}
