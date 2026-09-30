float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float t0 = dot(sv_position.xy, sv_position.xy) + 1;
	return float4(-sv_position.xy / sqrt(t0), -rsqrt(t0), 1);
}
