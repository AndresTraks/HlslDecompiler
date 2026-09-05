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

	float2 r0;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= 3) ? -1 : 0;
		if (r0.y != 0) break;
		o.sv_position = i[r0.x].sv_position;
		o.normal = i[r0.x].normal.xyz;
		stream.Append(o);
		r0.x = r0.x + 1;
	}
}
