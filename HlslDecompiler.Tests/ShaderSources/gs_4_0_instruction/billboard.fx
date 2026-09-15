float4x4 viewProj;
float3 right;
float3 up;
float size;

struct GS_IN
{
	float3 position : POSITION;
	float4 color : COLOR;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	float4 r0;
	float4 r1;
	float r2;
	r0.xyz = -(right.xyz) * size + i[0].position.xyz;
	r1.xyz = -(up.xyz) * size + r0.xyz;
	r0.xyz = up.xyz * size + r0.xyz;
	r1.w = 1;
	r2 = dot(r1, transpose(viewProj)[0]);
	o.sv_position.x = r2.x;
	r2 = dot(r1, transpose(viewProj)[1]);
	o.sv_position.y = r2.x;
	r2 = dot(r1, transpose(viewProj)[2]);
	r1.x = dot(r1, transpose(viewProj)[3]);
	o.sv_position.z = r2.x;
	o.sv_position.w = r1.x;
	o.texcoord = float2(0, 1);
	o.color = i[0].color;
	stream.Append(o);
	r0.w = 1;
	r1.x = dot(r0, transpose(viewProj)[0]);
	o.sv_position.x = r1.x;
	r1.x = dot(r0, transpose(viewProj)[1]);
	o.sv_position.y = r1.x;
	r1.x = dot(r0, transpose(viewProj)[2]);
	r0.x = dot(r0, transpose(viewProj)[3]);
	o.sv_position.z = r1.x;
	o.sv_position.w = r0.x;
	o.texcoord = float2(0, 0);
	o.color = i[0].color;
	stream.Append(o);
	r0.xyz = right.xyz * size + i[0].position.xyz;
	r1.xyz = -(up.xyz) * size + r0.xyz;
	r0.xyz = up.xyz * size + r0.xyz;
	r1.w = 1;
	r2 = dot(r1, transpose(viewProj)[0]);
	o.sv_position.x = r2.x;
	r2 = dot(r1, transpose(viewProj)[1]);
	o.sv_position.y = r2.x;
	r2 = dot(r1, transpose(viewProj)[2]);
	r1.x = dot(r1, transpose(viewProj)[3]);
	o.sv_position.z = r2.x;
	o.sv_position.w = r1.x;
	o.texcoord = float2(1, 1);
	o.color = i[0].color;
	stream.Append(o);
	r0.w = 1;
	r1.x = dot(r0, transpose(viewProj)[0]);
	o.sv_position.x = r1.x;
	r1.x = dot(r0, transpose(viewProj)[1]);
	o.sv_position.y = r1.x;
	r1.x = dot(r0, transpose(viewProj)[2]);
	r0.x = dot(r0, transpose(viewProj)[3]);
	o.sv_position.z = r1.x;
	o.sv_position.w = r0.x;
	o.texcoord = float2(1, 0);
	o.color = i[0].color;
	stream.Append(o);
}
