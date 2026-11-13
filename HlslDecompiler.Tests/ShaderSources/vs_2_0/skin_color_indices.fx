float4x3 bones[40];
float4 palette[16];
float4x4 viewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
	float4 blendindices1 : BLENDINDICES1;
};

struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 t0 = mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).x]) * i.blendweight.x + mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).y]) * i.blendweight.y + mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).z]) * i.blendweight.z + mul(i.position, bones[D3DCOLORtoUBYTE4(i.blendindices).w]) * i.blendweight.w;
	o.color = palette[D3DCOLORtoUBYTE4(i.blendindices1).y];
	o.position = mul(float4(t0, 1), viewProj);

	return o;
}
