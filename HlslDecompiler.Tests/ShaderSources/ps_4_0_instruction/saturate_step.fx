float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = float4(1, 1, 1, 1) / texcoord;
	r0 = saturate(r0);
	r1 = asfloat((float4(0.5, 0.5, 0.5, 0.5) >= texcoord) ? -1 : 0);
	r1 = asfloat(asint(r1) & asint(float4(1, 1, 1, 1)));
	o = r0 + r1;

	return o;
}
