float4 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float2 r1;
	float4 r2;
	float r3;
	r0.x = k.x * texcoord.x;
	r0.x = r0.x * 0.15915494 + 0.5;
	r0.x = frac(r0.x);
	r0.x = r0.x * 6.2831855 + -3.1415927;
	sincos(r0.x, r1.y, r1.x);
	r0.xy = r1.yx;
	r1 = texcoord.yz * 0.15915494 + 0.5;
	r1 = frac(r1.xy);
	r1 = r1.xy * 6.2831855 + -3.1415927;
	r2.y = sin(r1.x);
	r3.x = cos(r1.y);
	r0.w = r3.x;
	r0.z = r2.y;
	o = r0 * k;

	return o;
}
