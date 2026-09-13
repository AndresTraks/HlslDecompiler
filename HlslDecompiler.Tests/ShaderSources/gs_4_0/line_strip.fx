float4 offset;

struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

[maxvertexcount(4)]
void main(line GS_IN i[2], inout LineStream<GS_OUT> stream)
{
	GS_OUT o;

	o.sv_position = i[0].sv_position;
	o.color = float4(1, 0, 0, 1);
	stream.Append(o);
	o.sv_position = i[1].sv_position;
	o.color = float4(1, 0, 0, 1);
	stream.Append(o);
	stream.RestartStrip();
	o.sv_position = i[0].sv_position + offset;
	o.color = float4(0, 1, 0, 1);
	stream.Append(o);
	o.sv_position = i[1].sv_position + offset;
	o.color = float4(0, 1, 0, 1);
	stream.Append(o);
}
