float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float t0 = clamp(sv_position.x, -1, 1);
	float t1 = ((-0.0187292993 * abs(t0) + 0.0742610022) * abs(t0) - 0.212114394) * abs(t0) + 1.57072878;
	float t2 = sqrt(1 - abs(t0));
	float t3 = t0 < 0 ? -2 * t2 * t1 + 3.14159274 : 0;
	float2 t4 = abs(sv_position.x);
	float2 t5 = min(t4, float2(1, 2)) / max(t4, float2(1, 2));
	float2 t6 = t5 * t5;
	float2 t7 = t6 * (t6 * (t6 * (0.0208350997 * t6 - 0.0851330012) + 0.180141002) - 0.330299497) + 0.999866009;
	float2 t8 = t5 * t7 + (t4 > float2(1, 2) ? -2 * t7 * t5 + 1.57079637 : 0);
	return float4(min(sv_position.x, 1) < 0 ? -t8.x : t8.x, 1.57079637 - (t1 * t2 + t3), t1 * t2 + t3, min(sv_position.x, 2) < 0 ? -t8.y : t8.y);
}
