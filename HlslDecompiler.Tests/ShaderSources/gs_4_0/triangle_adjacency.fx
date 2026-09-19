struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
};

[maxvertexcount(3)]
void main(triangleadj GS_IN i[6], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 6; t0 = t0 + 2) {
		o.sv_position = i[t0].sv_position;
		stream.Append(o);
	}
}
