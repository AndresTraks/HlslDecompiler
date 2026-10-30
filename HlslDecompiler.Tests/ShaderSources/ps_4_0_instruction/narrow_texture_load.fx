Texture2D<uint2> stencil;
Texture2DMS<float4, 4> color;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	float4 r2;
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r1 = color.Load(r0.xy, 0);
	r2 = color.Load(r0.xy, 1);
	r1 = r1.zyxw + r2.zyxw;
	r2 = color.Load(r0.xy, 2);
	r1 = r1 + r2.zyxw;
	r2 = color.Load(r0.xy, 3);
	r0.xy = stencil.Load(r0.xyw);
	r0.zw = uint2(0, 1);
	r0.x = r0.y & 128;
	r1 = r1 + r2.zyxw;
	r1 = r1 * float4(0.25, 0.25, 0.25, 0.25);
	o = (r0.x != 0) ? r1 : r1.zyxw;

	return o;
}
