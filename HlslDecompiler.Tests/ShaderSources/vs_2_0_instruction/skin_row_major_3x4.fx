row_major float3x4 bones[40];
float4x4 viewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 position : POSITION;
	float3 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	int2 a0;
	float4 r1;
	float4 r2;
	float4 r3;
	r0.xy = i.blendindices.zy * 765.00586;
	a0 = r0.yx;
	r0 = i.blendweight.y * bones[a0.x / 3][0];
	r0 = bones[a0.y / 3][0] * i.blendweight.x + r0;
	r1.x = dot(r0, i.position);
	r0.x = dot(r0.xyz, i.normal.xyz);
	r2 = i.blendweight.y * bones[a0.x / 3][1];
	r3 = i.blendweight.y * bones[a0.x / 3][2];
	r3 = bones[a0.y / 3][2] * i.blendweight.x + r3;
	r2 = bones[a0.y / 3][1] * i.blendweight.x + r2;
	r1.y = dot(r2, i.position);
	r0.y = dot(r2.xyz, i.normal.xyz);
	r1.z = dot(r3, i.position);
	r0.z = dot(r3.xyz, i.normal.xyz);
	r1.w = 1;
	o.position.x = dot(r1, transpose(viewProj)[0]);
	o.position.y = dot(r1, transpose(viewProj)[1]);
	o.position.z = dot(r1, transpose(viewProj)[2]);
	o.position.w = dot(r1, transpose(viewProj)[3]);
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	o.texcoord = r0.www * r0.xyz;

	return o;
}
