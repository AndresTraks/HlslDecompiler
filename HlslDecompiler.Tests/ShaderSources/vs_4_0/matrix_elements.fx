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

	float3 t0 = i.position - pivot;
	o.sv_position = mul(float4(t0.x * world[0][0] + world[0][3], t0.y * world[1][1] + world[1][3], t0.z * world[2][2] + world[2][3], 1), viewProjection);
	o.texcoord = i.texcoord;
	o.texcoord1 = world[2][2] * world[2][3];

	return o;
}
