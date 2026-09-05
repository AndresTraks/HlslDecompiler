float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = -(texcoord) + texcoord.wzyx;
	r0 = r0 * float4(0.25, 0.25, 0.25, 0.25) + texcoord;
	r1 = max(texcoord, float4(0.100000001, 0.100000001, 0.100000001, 0.100000001));
	r1 = min(r1, float4(0.899999976, 0.899999976, 0.899999976, 0.899999976));
	r0 = r0 + r1;
	r1 = min(texcoord, float4(0.5, 0.5, 0.5, 0.5));
	r0 = r0 + r1;
	r1 = max(texcoord, float4(0.200000003, 0.200000003, 0.200000003, 0.200000003));
	o = r0 + r1;

	return o;
}
