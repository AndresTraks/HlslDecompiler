float cutoff;
sampler2D samp;

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
	r0 = tex2D(samp, i.texcoord.xy);
	r1.x = r0.w + -cutoff.x;
	r1 = (r1.x >= 0) ? -0 : -1;
	clip(r1);
	r1.x = i.texcoord1.x * 0.15915494 + 0.5;
	r1.x = frac(r1.x);
	r1.x = r1.x * 6.2831855 + -3.1415927;
	sincos(r1.x, r2.y, r2.x);
	r1.xy = r2.yx;
	r1.z = ddx(i.texcoord.x);
	r1.w = ddy(i.texcoord.y);
	r1 = -r0 + r1;
	o = r0.w * r1 + r0;

	return o;
}
