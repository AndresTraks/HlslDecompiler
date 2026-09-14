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

	float t0 = dot(i.position, transpose(bones[i.blendindices.x])[0]) * i.blendweight.x + dot(i.position, transpose(bones[i.blendindices.y])[0]) * i.blendweight.y;
	float t1 = dot(i.position, transpose(bones[i.blendindices.x])[1]) * i.blendweight.x + dot(i.position, transpose(bones[i.blendindices.y])[1]) * i.blendweight.y;
	float t2 = dot(i.position, transpose(bones[i.blendindices.x])[2]) * i.blendweight.x + dot(i.position, transpose(bones[i.blendindices.y])[2]) * i.blendweight.y;
	float t3 = dot(i.normal.xyz, transpose(bones[i.blendindices.x])[0].xyz) * i.blendweight.x + dot(i.normal.xyz, transpose(bones[i.blendindices.y])[0].xyz) * i.blendweight.y;
	float t4 = dot(i.normal.xyz, transpose(bones[i.blendindices.x])[1].xyz) * i.blendweight.x + dot(i.normal.xyz, transpose(bones[i.blendindices.y])[1].xyz) * i.blendweight.y;
	float t5 = dot(i.normal.xyz, transpose(bones[i.blendindices.x])[2].xyz) * i.blendweight.x + dot(i.normal.xyz, transpose(bones[i.blendindices.y])[2].xyz) * i.blendweight.y;
	float t6 = sqrt(t3 * t3 + t4 * t4 + t5 * t5);
	o.position = mul(float4(t0, t1, t2, 1), vp);
	o.texcoord = float3(t3, t4, t5) / t6;

	return o;
}
