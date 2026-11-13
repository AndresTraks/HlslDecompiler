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
	float3 t0 = mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).x]) * i.blendweight.x + (1 - i.blendweight.x) * mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).y]);
	return mul(float4(t0, 1), viewProj);
}
