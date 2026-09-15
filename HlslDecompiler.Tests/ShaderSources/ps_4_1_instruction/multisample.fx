Texture2DMS<float4, 4> tex;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	int2 r2;
	float4 r3;
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r1 = float4(0, 0, 0, 0);
	r2.x = 0;
	[loop]
	while (true) {
		r2.y = (r2.x >= 4) ? -1 : 0;
		if (r2.y != 0) break;
		r3 = tex.Load(r0.xy, r2.x);
		r1 = r1 + r3;
		r2.x = r2.x + 1;
	}
	o = r1 * float4(0.25, 0.25, 0.25, 0.25);

	return o;
}
