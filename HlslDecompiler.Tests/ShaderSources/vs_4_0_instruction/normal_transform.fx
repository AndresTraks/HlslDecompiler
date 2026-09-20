float4x4 world;
float4x4 viewProj;
float4 clipPlane;
float3 lightDir;

struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
	float sv_clipdistance : SV_ClipDistance;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float3 r1;
	float3 r2;
	r0.x = dot(i.position, transpose(world)[0]);
	r0.y = dot(i.position, transpose(world)[1]);
	r0.z = dot(i.position, transpose(world)[2]);
	r0.w = dot(i.position, transpose(world)[3]);
	o.sv_position.x = dot(r0, transpose(viewProj)[0]);
	o.sv_position.y = dot(r0, transpose(viewProj)[1]);
	o.sv_position.z = dot(r0, transpose(viewProj)[2]);
	o.sv_position.w = dot(r0, transpose(viewProj)[3]);
	o.sv_clipdistance = dot(r0, clipPlane);
	r0.xyz = i.normal.yyy * transpose(world)[1].xyz;
	r0.xyz = transpose(world)[0].xyz * i.normal.xxx + r0.xyz;
	r0.xyz = transpose(world)[2].xyz * i.normal.zzz + r0.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	o.normal = r0.xyz;
	r1.x = dot(i.tangent.xyz, transpose(world)[0].xyz);
	r1.y = dot(i.tangent.xyz, transpose(world)[1].xyz);
	r1.z = dot(i.tangent.xyz, transpose(world)[2].xyz);
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = rsqrt(r0.w);
	r1 = r0.www * r1.xyz;
	r2 = r0.zxy * r1.yzx;
	r2 = r0.yzx * r1.zxy + -(r2.xyz);
	o.texcoord.z = dot(r0.xyz, lightDir.xyz);
	o.texcoord.x = dot(r1.xyz, lightDir.xyz);
	o.texcoord.y = dot(r2.xyz, lightDir.xyz);

	return o;
}
