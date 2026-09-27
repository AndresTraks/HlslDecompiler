Texture2D tex;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	return float4(tex.Load(int3((int2)sv_position.xy, 0)).y, tex.Load(int3((int2)(2 * sv_position.xy), 0)).z, 0, 1);
}
