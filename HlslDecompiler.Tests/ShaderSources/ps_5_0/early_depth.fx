[earlydepthstencil]
float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	clip(sv_position.w - 0.5);
	return 2 * sv_position;
}
