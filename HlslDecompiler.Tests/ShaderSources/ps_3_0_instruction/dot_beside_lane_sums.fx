float4 c;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2D(s0, texcoord.xy);
	o.x = dot(r0.xyz, c.xyz);
	r0 = r0 * c;
	o.yz = r0.zw + r0.yx;
	o.w = r0.w;

	return o;
}
