cbuffer Params : register(b0)
{
	float4x4 faces[6];
};

struct GS_IN
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	uint sv_rendertargetarrayindex : SV_RenderTargetArrayIndex;
};

[maxvertexcount(3)]
[instance(6)]
void main(triangle GS_IN i[3], uint sv_gsinstanceid : SV_GSInstanceID, inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	o.sv_position = mul(i[0].sv_position, faces[sv_gsinstanceid]);
	o.normal = i[0].normal;
	o.sv_rendertargetarrayindex = sv_gsinstanceid;
	stream.Append(o);
	o.sv_position = mul(i[1].sv_position, faces[sv_gsinstanceid]);
	o.normal = i[1].normal;
	o.sv_rendertargetarrayindex = sv_gsinstanceid;
	stream.Append(o);
	o.sv_position = mul(i[2].sv_position, faces[sv_gsinstanceid]);
	o.normal = i[2].normal;
	o.sv_rendertargetarrayindex = sv_gsinstanceid;
	stream.Append(o);
}
