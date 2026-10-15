struct GS_IN
{
	float4 position : POSITION;
	uint id : ID;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float f : F;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 3; t0 = t0 + 1) {
		o.sv_position = i[t0].position;
		o.f = i[t0].position.x > 0 ? asfloat(i[t0].id) : i[t0].position.y;
		stream.Append(o);
	}
}
