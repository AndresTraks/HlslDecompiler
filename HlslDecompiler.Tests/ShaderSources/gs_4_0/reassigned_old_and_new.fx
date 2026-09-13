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

[maxvertexcount(3)]
void main(point GS_IN i[1], inout PointStream<GS_OUT> stream)
{
	GS_OUT o;

	float4 t0 = i[0].sv_position + offset;
	o.sv_position = t0;
	o.color = 0;
	stream.Append(o);
	o.sv_position = 3 * t0;
	t0 = sqrt(t0);
	o.color = t0 + i[0].sv_position;
	stream.Append(o);
	o.sv_position = t0 * t0;
	o.color = t0 * offset;
	stream.Append(o);
}
