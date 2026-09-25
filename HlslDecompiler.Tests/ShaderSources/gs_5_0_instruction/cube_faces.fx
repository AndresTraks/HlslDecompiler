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

	float2 r0;
	r0.x = sv_gsinstanceid.x << 2;
	r0.y = dot(i[0].sv_position, transpose(faces[r0.x / 4])[0]);
	o.sv_position.x = r0.y;
	r0.y = dot(i[0].sv_position, transpose(faces[r0.x / 4])[1]);
	o.sv_position.y = r0.y;
	r0.y = dot(i[0].sv_position, transpose(faces[r0.x / 4])[2]);
	o.sv_position.z = r0.y;
	r0.y = dot(i[0].sv_position, transpose(faces[r0.x / 4])[3]);
	o.sv_position.w = r0.y;
	o.normal = i[0].normal.xyz;
	o.sv_rendertargetarrayindex = sv_gsinstanceid.x;
	stream.Append(o);
	r0.y = dot(i[1].sv_position, transpose(faces[r0.x / 4])[0]);
	o.sv_position.x = r0.y;
	r0.y = dot(i[1].sv_position, transpose(faces[r0.x / 4])[1]);
	o.sv_position.y = r0.y;
	r0.y = dot(i[1].sv_position, transpose(faces[r0.x / 4])[2]);
	o.sv_position.z = r0.y;
	r0.y = dot(i[1].sv_position, transpose(faces[r0.x / 4])[3]);
	o.sv_position.w = r0.y;
	o.normal = i[1].normal.xyz;
	o.sv_rendertargetarrayindex = sv_gsinstanceid.x;
	stream.Append(o);
	r0.y = dot(i[2].sv_position, transpose(faces[r0.x / 4])[0]);
	o.sv_position.x = r0.y;
	r0.y = dot(i[2].sv_position, transpose(faces[r0.x / 4])[1]);
	o.sv_position.y = r0.y;
	r0.y = dot(i[2].sv_position, transpose(faces[r0.x / 4])[2]);
	r0.x = dot(i[2].sv_position, transpose(faces[r0.x / 4])[3]);
	o.sv_position.z = r0.y;
	o.sv_position.w = r0.x;
	o.normal = i[2].normal.xyz;
	o.sv_rendertargetarrayindex = sv_gsinstanceid.x;
	stream.Append(o);
}
