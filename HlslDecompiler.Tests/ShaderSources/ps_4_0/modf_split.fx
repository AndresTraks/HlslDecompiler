float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	return float4(sv_position.x - trunc(sv_position.x), trunc(sv_position.x), sv_position.x - trunc(sv_position.x), sv_position.x - trunc(sv_position.x));
}
