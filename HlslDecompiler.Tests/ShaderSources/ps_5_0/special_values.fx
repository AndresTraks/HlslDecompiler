float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	return float4(isinf(1000000015047466219876688855040.0 * sv_position.x) ? 1.0 : 0, 0, 0, 1);
}
