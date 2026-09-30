float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int r0;
	r0 = asint(sv_position.x * 1000000015047466219876688855040.0);
	r0 = r0.x & 2147483647;
	r0 = (r0.x == 2139095040) ? -1 : 0;
	o.x = asfloat(r0.x & 1065353216);
	o.yzw = float3(0, 0, 1);

	return o;
}
