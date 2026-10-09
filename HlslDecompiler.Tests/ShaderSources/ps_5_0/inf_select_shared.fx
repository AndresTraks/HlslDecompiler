float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int t0 = isinf(1000000015047466219876688855040.0 * sv_position.x);
	return float4(asfloat(t0 ? 1065353216 : 0), 2 * asfloat(t0 ? 1065353216 : 0), 0, 1);
}
