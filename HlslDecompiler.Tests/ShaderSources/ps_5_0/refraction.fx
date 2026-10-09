float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float t0 = sqrt(dot(sv_position.xy, sv_position.xy) + 1);
	float2 t1 = sv_position.xy / t0;
	return float4(0.899999976 * t1.x, 0.899999976 * t1.y - (0.899999976 * t1.y + sqrt(-0.809999943 * (-t1.y * t1.y + 1) + 1)), 0.899999976 / t0, 1);
}
