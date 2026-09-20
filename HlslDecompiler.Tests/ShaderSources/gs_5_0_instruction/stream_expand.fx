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

	float2 r0;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= 3) ? -1 : 0;
		if (asint(r0.y) != 0) break;
		r0.y = 0.100000001 + i[r0.x].sv_position.x;
		o.sv_position.x = r0.y;
		o.sv_position.yzw = i[r0.x].sv_position.yzw;
		stream.Append(o);
		r0.x = r0.x + 1;
	}
	stream.RestartStrip();
}
