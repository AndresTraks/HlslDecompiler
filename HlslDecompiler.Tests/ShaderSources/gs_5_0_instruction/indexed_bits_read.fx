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

	int2 r0;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= 3) ? -1 : 0;
		if (r0.y != 0) break;
		r0.y = (0 < i[r0.x].position.x) ? -1 : 0;
		r0.y = (r0.y != 0) ? i[r0.x].id : asint(i[r0.x].position.y);
		o.sv_position = i[r0.x].position;
		o.f = asfloat(r0.y);
		stream.Append(o);
		r0.x = r0.x + 1;
	}
}
