float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float t0 = sqrt(dot(sv_position.xy, sv_position.xy) + 1);
	float t1 = sv_position.y / t0;
	return float4(0.899999976 * (sv_position.x / t0), 0.899999976 * t1 - (0.899999976 * t1 + sqrt(-0.809999943 * (-t1 * t1 + 1) + 1)), 0.899999976 / t0, 1);
}
