float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.x = 1 / -k.z;
	r0 = r0.x * texcoord.zwxy;
	r1 = frac(abs(r0));
	r0 = (r0 >= 0) ? r1 : -r1;
	r0 = r0 * -k.z;
	r1.x = 1 / k.y;
	r1 = r1.x * texcoord;
	r2 = frac(abs(r1));
	r1 = (r1 >= 0) ? r2 : -r2;
	r0 = r1 * k.y + r0;
	r1.x = 1 / k.x;
	r1 = r1.x * texcoord;
	r2 = frac(abs(r1));
	r1 = (r1 >= 0) ? r2 : -r2;
	r1 = r1 * -k.x + k.w;
	o = r0 + r1;

	return o;
}
