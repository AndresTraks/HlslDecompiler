struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 3; t0 = t0 + 1) {
		o.sv_position = float4(0.100000001 + i[t0].sv_position.x, i[t0].sv_position.yzw);
		stream.Append(o);
	}
	stream.RestartStrip();
}
