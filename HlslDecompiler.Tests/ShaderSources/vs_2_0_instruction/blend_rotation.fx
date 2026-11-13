float3x3 rot[40];
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
	o.position.x = dot(i.position, transpose(viewProj)[0]);
	o.position.y = dot(i.position, transpose(viewProj)[1]);
	o.position.z = dot(i.position, transpose(viewProj)[2]);
	o.position.w = dot(i.position, transpose(viewProj)[3]);
	r0.xy = i.blendindices.zy * 765.00586;
	a0 = r0.xy;
	r0.xy = i.blendweight.xx * transpose(rot[a0.x / 3])[1].xy;
	r1.w = r0.x;
	r2.xy = i.blendweight.yy * transpose(rot[a0.y / 3])[1].xy;
	r3.w = r2.x;
	r1.z = i.blendweight.x * transpose(rot[a0.x / 3])[2].x;
	r3.z = i.blendweight.y * transpose(rot[a0.y / 3])[2].x;
	r1.xy = i.blendweight.xx * transpose(rot[a0.x / 3])[0].xy;
	r3.xy = i.blendweight.yy * transpose(rot[a0.y / 3])[0].xy;
	r1.xzw = r1.xwz + r3.xwz;
	o.texcoord.x = dot(r1.xzw, i.normal.xyz);
	r1.xw = i.blendweight.xx * transpose(rot[a0.x / 3])[2].yz;
	r0.zw = r1.yx;
	r3.xw = i.blendweight.yy * transpose(rot[a0.y / 3])[2].yz;
	r2.zw = r3.yx;
	r0.xyz = r0.zyw + r2.zyw;
	o.texcoord.y = dot(r0.xyz, i.normal.xyz);
	r1.y = i.blendweight.x * transpose(rot[a0.x / 3])[0].z;
	r1.z = i.blendweight.x * transpose(rot[a0.x / 3])[1].z;
	r3.y = i.blendweight.y * transpose(rot[a0.y / 3])[0].z;
	r3.z = i.blendweight.y * transpose(rot[a0.y / 3])[1].z;
	r0.xyz = r1.yzw + r3.yzw;
	o.texcoord.z = dot(r0.xyz, i.normal.xyz);

	return o;
}
