Texture2DMS<float4, 4> tex;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int2 t0 = (int2)sv_position.xy;
	float4 t1 = 0;
	for (int t2 = 0; t2 < 4; t2 = t2 + 1) {
		t1 = t1 + tex.Load(t0, t2);
	}
	return 0.25 * t1;
}
