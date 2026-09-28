float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.x = 1 / -k.z;
	r0 = r0.x * texcoord;
	r1 = frac(abs(r0));
	r0 = (r0 >= 0) ? r1 : -r1;
	o = r0 * k.z + k.w;

	return o;
}
