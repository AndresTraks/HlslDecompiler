struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

[maxvertexcount(8)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	int4 r0;
	float4 r1;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= 3) ? -1 : 0;
		if (r0.y != 0) break;
		r0.y = asint((float)r0.x);
		r0.z = 0;
		while (true) {
			r0.w = (r0.z >= 2) ? -1 : 0;
			if (r0.w != 0) break;
			r1 = asfloat(r0.y) + i[r0.z].sv_position;
			r0.w = -r0.z;
			o.sv_position = r1;
			o.color = i[r0.w + 2].color;
			stream.Append(o);
			r0.z = r0.z + 1;
		}
		r0.y = (r0.x == 1) ? -1 : 0;
		if (r0.y != 0) {
			stream.RestartStrip();
		}
		r0.x = r0.x + 1;
	}
	o.sv_position = i[0].sv_position;
	o.color = i[0].color;
	stream.Append(o);
	stream.RestartStrip();
}
