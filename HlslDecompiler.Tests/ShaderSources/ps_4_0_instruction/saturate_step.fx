float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = r0;
	r1 = (float4(0.5, 0.5, 0.5, 0.5) >= texcoord) ? -1 : 0;
	o = r0 + r1;

	return o;
}
