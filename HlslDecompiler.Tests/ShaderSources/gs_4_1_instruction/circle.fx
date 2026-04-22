struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

struct GS_OUT
{
	float4 sv_target : SV_Target;
	float4 color : COLOR;
};

[maxvertexcount(17)]
void main(point GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	float4 r0;
	float2 r1;
	float4 r2;
	o.sv_target = i[0].sv_position;
	o.color = i[0].color;
	stream.Append(o);
	r0.zw = float2(0, 0);
	r1.x = 0.000000;
	while (true) {
		r1.y = (17 < r1.x) ? -1 : 0;
		if (r1.x != 0) break;
		r1.y = r1.x
		r1.y = r1.y * 0.392699093;
		r2.x = sin(r1.y);
		r0.x = cos(r1.y);
		r0.y = r2.x;
		r2 = r0 * float4(0.5, 0.5, 0, 0) + i[0].sv_position;
		o.sv_target = r2;
		o.color = i[0].color;
		stream.Append(o);
		r1.x = r1.x + 1;
	}
}
