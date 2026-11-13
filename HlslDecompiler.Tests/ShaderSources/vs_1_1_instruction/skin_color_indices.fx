float4x3 bones[20];
float4x4 viewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : POSITION
{
	float4 o;

	float4 r0;
	int a0;
	float3 r1;
	r0.x = -i.blendweight.x + 1;
	r0.yz = i.blendindices.zy * 765.00586;
	a0 = r0.z;
	r1.x = dot(i.position, transpose(bones[a0 / 3])[0]);
	r1.y = dot(i.position, transpose(bones[a0 / 3])[1]);
	r1.z = dot(i.position, transpose(bones[a0 / 3])[2]);
	r0.xzw = r0.xxx * r1.xyz;
	a0 = r0.y;
	r1.x = dot(i.position, transpose(bones[a0 / 3])[0]);
	r1.y = dot(i.position, transpose(bones[a0 / 3])[1]);
	r1.z = dot(i.position, transpose(bones[a0 / 3])[2]);
	r0.xyz = r1.xyz * i.blendweight.xxx + r0.xzw;
	r0.w = 1;
	o.x = dot(r0, transpose(viewProj)[0]);
	o.y = dot(r0, transpose(viewProj)[1]);
	o.z = dot(r0, transpose(viewProj)[2]);
	o.w = dot(r0, transpose(viewProj)[3]);

	return o;
}
