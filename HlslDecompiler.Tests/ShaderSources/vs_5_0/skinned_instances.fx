cbuffer PerFrame : register(b0)
{
	float4x4 viewProjection;
	float3 cameraPosition;
	float time;
};

cbuffer PerObject : register(b1)
{
	float4x4 world;
	float4x4 bones[32];
	float4 tint;
	uint boneOffset;
};

struct VS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
	uint4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
	uint sv_instanceid : SV_InstanceID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
	float fog : FOG;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = 0.5 * frac(0.617999971 * (float)i.sv_instanceid) + 0.5;
	int4 t1 = i.blendindices + boneOffset;
	float4x4 t2 = bones[t1.x] * i.blendweight.x + bones[t1.y] * i.blendweight.y + bones[t1.z] * i.blendweight.z + bones[t1.w] * i.blendweight.w;
	float4 t3 = mul(float4(i.position, 1), t2);
	float3 t4 = float3(dot(transpose(world)[0], t3), dot(transpose(world)[2], t3), dot(transpose(world)[3], t3));
	float t5 = 0.100000001 * sin(0.5 * (float)i.sv_instanceid + time) + dot(transpose(world)[1], t3);
	o.sv_position = mul(float4(t4.x, t5, t4.yz), viewProjection);
	o.normal = normalize(mul(mul(i.normal, (float3x3)t2), (float3x3)world));
	o.texcoord = float2(0.00999999978 * time + i.texcoord.x, i.texcoord.y);
	o.fog = max(-0.00999999978 * length(float3(t4.x, t5, t4.y) - cameraPosition) + 1, 0);
	o.color = t0 * tint;

	return o;
}
