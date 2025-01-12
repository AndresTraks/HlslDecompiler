sampler2D sampler0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0.xy = texcoord.yx + texcoord.yx;
	r0 = tex2D(sampler0, r0.xy);
	o = r0.wzyx;

	return o;
}
