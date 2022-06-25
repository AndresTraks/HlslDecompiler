float4 main(float4 sv_position : TEXCOORD) : SV_Target
{
	return float4(3 * sv_position.xw - 1, 8, abs(3 * sv_position.x));
}
