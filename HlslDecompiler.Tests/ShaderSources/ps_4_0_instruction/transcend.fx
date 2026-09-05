float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.x = texcoord.x * 1.44269502;
	r0.x = exp2(r0.x);
	r0.y = log2(texcoord.y);
	r0.x = r0.y * 0.693147182 + r0.x;
	r1 = log2(texcoord);
	r1 = r1 * float4(2.5, 2.5, 2.5, 2.5);
	r1 = exp2(r1);
	r0 = r0.x + r1;
	r1.x = sqrt(texcoord.z);
	r0 = r0 + r1.x;
	r1.x = frac(texcoord.w);
	o = r0 + r1.x;

	return o;
}
