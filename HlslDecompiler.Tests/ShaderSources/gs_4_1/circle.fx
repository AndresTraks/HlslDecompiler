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

[maxvertexcount(17)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	o.sv_position = i[0].sv_position;
	o.color = i[0].color;
	stream.Append(o);
	for (int t0 = 1; t0 <= 17; t0 = t0 + 1) {
		o.sv_position = float4(0.5 * cos(0.392699093 * t0) + i[0].sv_position.x, 0.5 * sin(0.392699093 * t0) + i[0].sv_position.y, i[0].sv_position.zw);
		o.color = i[0].color;
		stream.Append(o);
	}
}
