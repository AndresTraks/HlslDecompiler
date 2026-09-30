float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int t0 = sv_position.x != 0 ? (1056964608 & ~8388607) | (asint(sv_position.x) & 8388607) : 0;
	int t1 = (sv_position.x != 0 ? (asint(sv_position.x) & 2139095040) - 1056964608 : 0) >> 23;
	return float4((float)t0, (float)t1, (float)(t0 * 8), exp2((float)t1) * (float)t0);
}
