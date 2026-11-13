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

	float3x4 t0 = bones[D3DCOLORtoUBYTE4(i.blendindices).x] * i.blendweight.x + bones[D3DCOLORtoUBYTE4(i.blendindices).y] * i.blendweight.y;
	float3 t1 = mul(t0, i.position);
	o.position = mul(float4(t1, 1), viewProj);
	o.texcoord = normalize(mul((float3x3)t0, i.normal.xyz));

	return o;
}
