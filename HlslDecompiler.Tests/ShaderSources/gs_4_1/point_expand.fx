struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 4; t0 = t0 + 1) {
		o.sv_position = float4(t0 + i[0].sv_position.xy, i[0].sv_position.zw);
		o.texcoord = t0;
		stream.Append(o);
	}
	stream.RestartStrip();
}
