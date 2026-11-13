sampler2D s0;
sampler2D s1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float2 r2;
	r0 = tex2D(s0, i.texcoord.xy);
	if (0.5 < i.texcoord1.x) {
		r1.xy = ddx(i.texcoord.xy);
		r1.zw = ddy(i.texcoord.xy);
		r2 = 4 * i.texcoord.xy;
		r1 = r1 * 4;
		r1 = tex2Dgrad(s1, r2.xy, r1.xy, r1.zw);
		o = r0 * r1;
	} else {
		o = r0;
	}

	return o;
}
