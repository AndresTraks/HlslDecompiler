float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int t0 = asint(1000000015047466219876688855040.0 * sv_position.x) & 2147483647;
	return float4(asfloat(t0 == 2139095040 ? 1065353216 : 0), 2 * (asfloat(t0 == 2139095040 ? 1065353216 : 0)), 0, 1);
}
