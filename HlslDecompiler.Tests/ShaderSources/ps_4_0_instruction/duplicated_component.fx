SamplerState samplerState0;
Texture2D texture0;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = texture0.Sample(samplerState0, texcoord.xy);
	o.zw = r0.ww;
	o.xy = frac(texcoord.zz);

	return o;
}
