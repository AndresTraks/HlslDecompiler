float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = asfloat((texcoord >= float4(0.5, 0.5, 0.5, 0.5)) ? -1 : 0);
	r0 = asfloat(asint(r0) & asint(float4(1, 1, 1, 1)));
	r1 = asfloat((float4(0.25, 0.25, 0.25, 0.25) >= texcoord) ? -1 : 0);
	r1 = asfloat(asint(r1) & asint(float4(8, 8, 8, 8)));
	o = r0 * float4(2, 2, 2, 2) + r1;

	return o;
}
