struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

[maxvertexcount(8)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 3; t0 = t0 + 1) {
		float t1 = (float)t0;
		for (int t2 = 0; t2 < 2; t2 = t2 + 1) {
			o.sv_position = t1 + i[t2].sv_position;
			o.color = float4(i[2 - t2].color.x, i[2 - t2].color.y, i[2 - t2].color.z, i[2 - t2].color.w);
			stream.Append(o);
		}
		if (t0 == 1) {
			stream.RestartStrip();
		}
	}
	o.sv_position = i[0].sv_position;
	o.color = i[0].color;
	stream.Append(o);
	stream.RestartStrip();
}
