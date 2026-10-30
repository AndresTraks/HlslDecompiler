Texture2D<uint2> stencil;
Texture2DMS<float4, 4> color;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int2 t0 = (int2)sv_position.xy;
	float4 t1 = 0.25 * (color.Load(t0, 0) + color.Load(t0, 1) + color.Load(t0, 2) + color.Load(t0, 3));
	int t2 = stencil.Load(int3(t0, 0)).y & 128;
	return t2 ? t1.zyxw : t1;
}
