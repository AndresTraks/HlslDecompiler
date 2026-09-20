float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float r1;
	r0.x = ddx_coarse(texcoord.x);
	r0.y = ddy_coarse(texcoord.y);
	r0.z = ddx_fine(texcoord.x);
	r0.w = ddy_fine(texcoord.y);
	r1 = rcp(texcoord.x);
	o = r0 + r1.x;

	return o;
}
