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

[maxvertexcount(17)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream) {
	GS_OUT o;

	o.sv_position = i[0].sv_position;
	o.color = i[0].color;
	stream.Append(o);
	for(int j = 1; j <= 17; j++) {
		o.sv_position = i[0].sv_position + 0.5 * float4(cos(j * 0.392699093), sin(j * 0.392699093), 0, 0);
		o.color = i[0].color;
		stream.Append(o);
	}
}