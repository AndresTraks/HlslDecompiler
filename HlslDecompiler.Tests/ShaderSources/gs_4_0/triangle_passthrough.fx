struct GS_IN
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 3; t0 = t0 + 1) {
		o.sv_position = i[t0].sv_position;
		o.normal = i[t0].normal;
		stream.Append(o);
	}
}
