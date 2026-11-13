float4x3 bones[30];
float4x4 viewProj;

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

	float4x3 t0 = bones[D3DCOLORtoUBYTE4(i.blendindices).x] * i.blendweight.x + bones[D3DCOLORtoUBYTE4(i.blendindices).y] * i.blendweight.y;
	float3 t1 = mul(i.position, t0);
	o.position = mul(float4(t1, 1), viewProj);
	o.texcoord = normalize(mul(i.normal.xyz, (float3x3)t0));

	return o;
}
