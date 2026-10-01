float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int3 r0;
	r0.x = asint(texcoord.x / texcoord.y);
	r0.y = (0 < asfloat(r0.x)) ? -1 : 0;
	r0.z = (asfloat(r0.x) < 0) ? -1 : 0;
	r0.y = -(r0.y) + r0.z;
	o.w = (float)r0.y;
	r0.y = (asfloat(r0.x) != asfloat(r0.x)) ? -1 : 0;
	r0.xz = r0.xx & int2(2147483647, 2139095040);
	o.x = asfloat(r0.y & 1065353216);
	r0.x = (r0.x == 2139095040) ? -1 : 0;
	r0.y = (r0.z != 2139095040) ? -1 : 0;
	o.yz = asfloat(r0.xy & int2(1065353216, 1065353216));

	return o;
}
