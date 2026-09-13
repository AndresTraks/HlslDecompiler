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

	float4 r0;
	float4 r1;
	r0 = offset + i[0].sv_position;
	o.sv_position = r0;
	o.color = float4(0, 0, 0, 0);
	stream.Append(o);
	r1 = r0 * float4(3, 3, 3, 3);
	r0 = sqrt(r0);
	o.sv_position = r1;
	r1 = r0 + i[0].sv_position;
	o.color = r1;
	stream.Append(o);
	r1 = r0 * r0;
	r0 = r0 * offset;
	o.sv_position = r1;
	o.color = r0;
	stream.Append(o);
}
