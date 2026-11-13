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

	o.position = mul(i.position, viewProj);
	o.texcoord = mul(rot[D3DCOLORtoUBYTE4(i.blendindices).x] * i.blendweight.x + rot[D3DCOLORtoUBYTE4(i.blendindices).y] * i.blendweight.y, i.normal.xyz);

	return o;
}
