float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	return float4(asfloat(isinf(1000000015047466219876688855040.0 * sv_position.x) ? 1065353216 : 0) * float2(1, 2), 0, 1);
}
