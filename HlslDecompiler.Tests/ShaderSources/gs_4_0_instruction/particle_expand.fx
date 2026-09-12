float4x4 viewProj;
float3 right;
float3 up;

struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
	float psize : PSIZE;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
	float2 texcoord : TEXCOORD;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	float4 r0;
	float4 r1;
	float r2;
	r0.xyz = up.xyz * i[0].psize.xxx;
	r1.xyz = -(right.xyz) * i[0].psize.xxx + -(r0.xyz);
	r1.xyz = r1.xyz + i[0].sv_position.xyz;
	r1.w = 1;
	r0.w = dot(r1, transpose(viewProj)[0]);
	o.sv_position.x = r0.w;
	r0.w = dot(r1, transpose(viewProj)[1]);
	o.sv_position.y = r0.w;
	r0.w = dot(r1, transpose(viewProj)[2]);
	r1.x = dot(r1, transpose(viewProj)[3]);
	o.sv_position.z = r0.w;
	o.sv_position.w = r1.x;
	o.color = i[0].color;
	o.texcoord = float2(0, 0);
	stream.Append(o);
	r1.xyz = right.xyz * i[0].psize.xxx + -(r0.xyz);
	r0.xyz = right.xyz * i[0].psize.xxx + r0.xyz;
	r0.xyz = r0.xyz + i[0].sv_position.xyz;
	r1.xyz = r1.xyz + i[0].sv_position.xyz;
	r1.w = 1;
	r2 = dot(r1, transpose(viewProj)[0]);
	o.sv_position.x = r2.x;
	r2 = dot(r1, transpose(viewProj)[1]);
	o.sv_position.y = r2.x;
	r2 = dot(r1, transpose(viewProj)[2]);
	r1.x = dot(r1, transpose(viewProj)[3]);
	o.sv_position.z = r2.x;
	o.sv_position.w = r1.x;
	o.color = i[0].color;
	o.texcoord = float2(1, 0);
	stream.Append(o);
	r1.xyz = right.xyz * i[0].psize.xxx;
	r1.xyz = up.xyz * i[0].psize.xxx + -(r1.xyz);
	r1.xyz = r1.xyz + i[0].sv_position.xyz;
	r1.w = 1;
	r2 = dot(r1, transpose(viewProj)[0]);
	o.sv_position.x = r2.x;
	r2 = dot(r1, transpose(viewProj)[1]);
	o.sv_position.y = r2.x;
	r2 = dot(r1, transpose(viewProj)[2]);
	r1.x = dot(r1, transpose(viewProj)[3]);
	o.sv_position.z = r2.x;
	o.sv_position.w = r1.x;
	o.color = i[0].color;
	o.texcoord = float2(0, 1);
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
	o.color = i[0].color;
	o.texcoord = float2(1, 1);
	stream.Append(o);
}
