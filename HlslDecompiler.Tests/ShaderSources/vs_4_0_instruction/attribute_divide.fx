float4x4 worldViewProj;
uint columns;

struct VS_IN
{
	float3 position : POSITION;
	uint texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	int r1;
	float2 r2;
	r0.xyz = i.position.xyz;
	r0.w = 1;
	o.sv_position.x = dot(r0, transpose(worldViewProj)[0]);
	o.sv_position.y = dot(r0, transpose(worldViewProj)[1]);
	o.sv_position.z = dot(r0, transpose(worldViewProj)[2]);
	o.sv_position.w = dot(r0, transpose(worldViewProj)[3]);
	r0.x = (float)(uint)columns;
	r1 = (uint)i.texcoord / (uint)columns;
	r2.x = (uint)i.texcoord % (uint)columns;
	r2.x = (float)(uint)r2.x;
	r2.y = (float)(uint)r1.x;
	o.texcoord = r2.xy / r0.xx;

	return o;
}
