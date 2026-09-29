sampler2D sampler0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	o.xy = frac(texcoord.xx);
	r0 = tex2D(sampler0, texcoord.xy);
	o.zw = r0.ww;

	return o;
}
