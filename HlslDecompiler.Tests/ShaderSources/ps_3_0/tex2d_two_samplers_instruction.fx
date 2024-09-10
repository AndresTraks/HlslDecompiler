sampler sampler0;
sampler sampler1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2D(sampler1, texcoord.yxzw);
	r0.xy = r0.xy * 2 + texcoord.yx;
	r0 = tex2D(sampler0, r0);
	o = r0.wzyx;

	return o;
}
