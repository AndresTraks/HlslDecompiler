float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	r0 = float4(1, 1, 1, 1) / texcoord;
	r0 = saturate(r0);
	r1 = (float4(0.5, 0.5, 0.5, 0.5) >= texcoord) ? -1 : 0;
	r1 = r1 & int4(1065353216, 1065353216, 1065353216, 1065353216);
	o = r0 + asfloat(r1);

	return o;
}
