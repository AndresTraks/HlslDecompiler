struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint sv_rendertargetarrayindex : SV_RenderTargetArrayIndex;
};

[maxvertexcount(3)]
[instance(4)]
void main(triangle GS_IN i[3], uint sv_gsinstanceid : SV_GSInstanceID, inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 3; t0 = t0 + 1) {
		o.sv_position = i[t0].sv_position;
		o.sv_rendertargetarrayindex = sv_gsinstanceid;
		stream.Append(o);
	}
}
