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

	float3 t0 = mul(i.position, bones[i.blendindices.x]) * i.blendweight.x + mul(i.position, bones[i.blendindices.y]) * i.blendweight.y;
	o.position = mul(float4(t0, 1), vp);
	o.texcoord = normalize(mul(i.normal.xyz, (float3x3)bones[i.blendindices.x]) * i.blendweight.x + mul(i.normal.xyz, (float3x3)bones[i.blendindices.y]) * i.blendweight.y);

	return o;
}
