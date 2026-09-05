float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = -(texcoord) + texcoord.wzyx;
	r0 = r0 * float4(0.25, 0.25, 0.25, 0.25) + texcoord;
	r0 = r0 + r1;
	r0 = r0 + r1;
	o = r0 + r1;

	return o;
}
