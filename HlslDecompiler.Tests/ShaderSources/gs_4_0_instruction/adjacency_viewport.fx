struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint sv_viewportarrayindex : SV_ViewportArrayIndex;
};

[maxvertexcount(4)]
void main(lineadj GS_IN i[4], inout LineStream<GS_OUT> stream)
{
	GS_OUT o;

	float4 r0;
	o.sv_position = i[1].sv_position;
	o.sv_viewportarrayindex = 0;
	stream.Append(o);
	o.sv_position = i[2].sv_position;
	o.sv_viewportarrayindex = 0;
	stream.Append(o);
	stream.RestartStrip();
	r0 = i[1].sv_position + i[0].sv_position;
	r0 = r0 * float4(0.5, 0.5, 0.5, 0.5);
	o.sv_position = r0;
	o.sv_viewportarrayindex = 1;
	stream.Append(o);
	r0 = i[3].sv_position + i[2].sv_position;
	r0 = r0 * float4(0.5, 0.5, 0.5, 0.5);
	o.sv_position = r0;
	o.sv_viewportarrayindex = 1;
	stream.Append(o);
}
