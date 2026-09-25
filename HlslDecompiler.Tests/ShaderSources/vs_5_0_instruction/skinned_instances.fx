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

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	r0.x = (float)(uint)i.sv_instanceid;
	r0.y = r0.x * 0.5 + time;
	r0.x = r0.x * 0.617999971;
	r0.x = frac(r0.x);
	r0.x = r0.x * 0.5 + 0.5;
	o.color = r0.x * tint;
	r0.x = sin(r0.y);
	r1 = i.blendindices + boneOffset;
	r1 = (int4)r1 << int4(2, 2, 2, 2);
	r2 = i.blendweight.y * transpose(bones[r1.y / 4])[3];
	r2 = transpose(bones[r1.x / 4])[3] * i.blendweight.x + r2;
	r2 = transpose(bones[r1.z / 4])[3] * i.blendweight.z + r2;
	r2 = transpose(bones[r1.w / 4])[3] * i.blendweight.w + r2;
	r3.xyz = i.position.xyz;
	r3.w = 1;
	r2.w = dot(r3, r2);
	r4 = i.blendweight.y * transpose(bones[r1.y / 4])[0];
	r4 = transpose(bones[r1.x / 4])[0] * i.blendweight.x + r4;
	r4 = transpose(bones[r1.z / 4])[0] * i.blendweight.z + r4;
	r4 = transpose(bones[r1.w / 4])[0] * i.blendweight.w + r4;
	r2.x = dot(r3, r4);
	r4.x = dot(i.normal.xyz, r4.xyz);
	r5 = i.blendweight.y * transpose(bones[r1.y / 4])[1];
	r5 = transpose(bones[r1.x / 4])[1] * i.blendweight.x + r5;
	r5 = transpose(bones[r1.z / 4])[1] * i.blendweight.z + r5;
	r5 = transpose(bones[r1.w / 4])[1] * i.blendweight.w + r5;
	r2.y = dot(r3, r5);
	r4.y = dot(i.normal.xyz, r5.xyz);
	r5 = i.blendweight.y * transpose(bones[r1.y / 4])[2];
	r5 = transpose(bones[r1.x / 4])[2] * i.blendweight.x + r5;
	r5 = transpose(bones[r1.z / 4])[2] * i.blendweight.z + r5;
	r1 = transpose(bones[r1.w / 4])[2] * i.blendweight.w + r5;
	r2.z = dot(r3, r1);
	r4.z = dot(i.normal.xyz, r1.xyz);
	r0.y = dot(r2, transpose(world)[1]);
	r0.y = r0.x * 0.100000001 + r0.y;
	r0.w = dot(r2, transpose(world)[3]);
	r0.x = dot(r2, transpose(world)[0]);
	r0.z = dot(r2, transpose(world)[2]);
	o.sv_position.x = dot(r0, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r0, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r0, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r0, transpose(viewProjection)[3]);
	r0.xyz = r0.xyz + -(cameraPosition.xyz);
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = -(r0.x) * 0.00999999978 + 1;
	o.fog = max(r0.x, 0);
	r0.x = dot(r4.xyz, transpose(world)[0].xyz);
	r0.y = dot(r4.xyz, transpose(world)[1].xyz);
	r0.z = dot(r4.xyz, transpose(world)[2].xyz);
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	o.normal = r0.www * r0.xyz;
	r0.x = time * 0.00999999978;
	r0.y = 0;
	o.texcoord = r0.xy + i.texcoord.xy;

	return o;
}
