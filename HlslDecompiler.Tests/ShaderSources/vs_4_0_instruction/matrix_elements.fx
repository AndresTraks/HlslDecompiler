cbuffer Transforms : register(b0)
{
	float4x4 world;
	float4x4 viewProjection;
	float3 pivot;
};

struct VS_IN
{
	float3 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float3 r1;
	r0.xyz = i.position.xyz + -(pivot.xyz);
	r1.x = r0.x * transpose(world)[0].x;
	r1.y = r0.y * transpose(world)[1].y;
	r1.z = r0.z * transpose(world)[2].z;
	r0.xyz = r1.xyz + transpose(world)[3].xyz;
	r0.w = 1;
	o.sv_position.x = dot(r0, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r0, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r0, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r0, transpose(viewProjection)[3]);
	o.texcoord1 = transpose(world)[2].z * transpose(world)[3].z;
	o.texcoord = i.texcoord.xy;

	return o;
}
