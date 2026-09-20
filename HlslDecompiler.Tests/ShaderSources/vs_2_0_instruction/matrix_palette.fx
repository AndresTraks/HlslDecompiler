float4x3 bones[8];
float4x4 vp;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
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
	float3 r1;
	r0.xy = i.blendindices.xy * 3;
	a0 = r0.yx;
	r0.x = dot(i.position, transpose(bones[a0.x / 3])[0]);
	r0.y = dot(i.position, transpose(bones[a0.x / 3])[1]);
	r0.z = dot(i.position, transpose(bones[a0.x / 3])[2]);
	r0.xyz = r0.xyz * i.blendweight.yyy;
	r1.x = dot(i.position, transpose(bones[a0.y / 3])[0]);
	r1.y = dot(i.position, transpose(bones[a0.y / 3])[1]);
	r1.z = dot(i.position, transpose(bones[a0.y / 3])[2]);
	r0.xyz = r1.xyz * i.blendweight.xxx + r0.xyz;
	r0.w = 1;
	o.position.x = dot(r0, transpose(vp)[0]);
	o.position.y = dot(r0, transpose(vp)[1]);
	o.position.z = dot(r0, transpose(vp)[2]);
	o.position.w = dot(r0, transpose(vp)[3]);
	r0.x = dot(i.normal.xyz, transpose(bones[a0.x / 3])[0].xyz);
	r0.y = dot(i.normal.xyz, transpose(bones[a0.x / 3])[1].xyz);
	r0.z = dot(i.normal.xyz, transpose(bones[a0.x / 3])[2].xyz);
	r0.xyz = r0.xyz * i.blendweight.yyy;
	r1.x = dot(i.normal.xyz, transpose(bones[a0.y / 3])[0].xyz);
	r1.y = dot(i.normal.xyz, transpose(bones[a0.y / 3])[1].xyz);
	r1.z = dot(i.normal.xyz, transpose(bones[a0.y / 3])[2].xyz);
	r0.xyz = r1.xyz * i.blendweight.xxx + r0.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	o.texcoord = r0.www * r0.xyz;

	return o;
}
