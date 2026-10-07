Texture2DMS<float4, 4> tex;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	int2 t0 = (int2)i.sv_position.xy;
	float4 t1 = 0;
	for (int t2 = 0; t2 < 4; t2 = t2 + 1) {
		t1 = t1 + tex.Load(t0, t2);
	}
	return 0.25 * t1;
}
