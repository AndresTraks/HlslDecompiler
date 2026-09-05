sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0 = tex2D(sampler0, texcoord.xy);
	if (-texcoord.x < 0) {
		r1 = tex2D(sampler0, texcoord.zw);
		r1 = r0 + r1;
	} else {
		r2 = tex2D(sampler0, texcoord.wz);
		r1 = r0 + -r2;
	}
	if (-texcoord.y < 0) {
		r0 = tex2D(sampler0, texcoord.yx);
		o = r0 * r1;
	} else {
		r0 = tex2D(sampler0, texcoord.xz);
		o = r0 + r1;
	}

	return o;
}
