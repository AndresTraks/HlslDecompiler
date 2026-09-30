float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	return float4((asint(1000000015047466219876688855040.0 * sv_position.x) & 2147483647) == 2139095040 ? 1.0 : 0, 0, 0, 1);
}
