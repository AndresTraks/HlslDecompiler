cbuffer c : register(b0)
{
	int slice;
	int2 off;
};

Texture2DArray t;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int2 t0 = (int2)sv_position.xy;
	return 0.5 * t.Load(int4(t0 + off, slice + 1, 1)) + t.Load(int4(t0, slice, 0), int2(1, -1)) - t.Load(int4(t0, slice, 0));
}
