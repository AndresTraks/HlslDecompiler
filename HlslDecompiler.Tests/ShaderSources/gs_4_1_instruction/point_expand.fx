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

	float4 r0;
	float2 r1;
	float4 r2;
	r0.zw = float2(0, 0);
	r1.x = 0;
	while (true) {
		if (r1.y != 0) break;
		r0.xy = r1.xx;
		r2 = r0 + i[0].sv_position;
		o.sv_position = r2;
		o.texcoord = r0.yy;
		stream.Append(o);
		r1.x = r1.x + 1;
	}
	stream.RestartStrip();
}
