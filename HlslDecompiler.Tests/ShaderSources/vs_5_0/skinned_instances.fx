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
	float4 t2 = transpose(bones[t1.x])[0] * i.blendweight.x + i.blendweight.y * transpose(bones[t1.y])[0] + transpose(bones[t1.z])[0] * i.blendweight.z + transpose(bones[t1.w])[0] * i.blendweight.w;
	float t3 = dot(i.normal, t2.xyz);
	float4 t4 = transpose(bones[t1.x])[1] * i.blendweight.x + i.blendweight.y * transpose(bones[t1.y])[1] + transpose(bones[t1.z])[1] * i.blendweight.z + transpose(bones[t1.w])[1] * i.blendweight.w;
	float t5 = dot(i.normal, t4.xyz);
	float4 t6 = transpose(bones[t1.x])[2] * i.blendweight.x + i.blendweight.y * transpose(bones[t1.y])[2] + transpose(bones[t1.z])[2] * i.blendweight.z + transpose(bones[t1.w])[2] * i.blendweight.w;
	float t7 = dot(i.normal, t6.xyz);
	float4 t8 = float4(dot(float4(i.position, 1), t2), dot(float4(i.position, 1), t4), dot(float4(i.position, 1), t6), dot(float4(i.position, 1), transpose(bones[t1.x])[3] * i.blendweight.x + i.blendweight.y * transpose(bones[t1.y])[3] + transpose(bones[t1.z])[3] * i.blendweight.z + transpose(bones[t1.w])[3] * i.blendweight.w));
	float3 t9 = float3(dot(transpose(world)[0], t8), dot(transpose(world)[2], t8), dot(transpose(world)[3], t8));
	float t10 = 0.100000001 * sin(0.5 * (float)i.sv_instanceid + time) + dot(transpose(world)[1], t8);
	o.sv_position = mul(float4(t9.x, t10, t9.yz), viewProjection);
	o.normal = normalize(mul(float3(t3, t5, t7), (float3x3)world));
	o.texcoord = float2(0.00999999978 * time + i.texcoord.x, i.texcoord.y);
	o.fog = max(-0.00999999978 * length(float3(t9.x, t10, t9.y) - cameraPosition) + 1, 0);
	o.color = t0 * tint;

	return o;
}
