struct GS_IN
{
	float4 sv_position : SV_Position;
	uint f : F;
	float w : W;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint f : F;
	float w : W;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (uint t0 = 0; t0 < 4; t0 = t0 + 1) {
		o.sv_position = float4(0.100000001 * (float2)uint2(t0 & 1, t0 >> 1) + i[0].sv_position.xy, i[0].sv_position.zw);
		o.f = i[0].f | (t0 * 256);
		o.w = (float)t0 * asfloat(i[0].f);
		stream.Append(o);
	}
}
