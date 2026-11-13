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

	float4 t0 = transpose(bones[D3DCOLORtoUBYTE4(i.blendindices).x])[0] * i.blendweight.x + i.blendweight.y * transpose(bones[D3DCOLORtoUBYTE4(i.blendindices).y])[0];
	float t1 = dot(i.position, t0);
	float t2 = dot(i.normal.xyz, t0.xyz);
	float4 t3 = transpose(bones[D3DCOLORtoUBYTE4(i.blendindices).x])[1] * i.blendweight.x + i.blendweight.y * transpose(bones[D3DCOLORtoUBYTE4(i.blendindices).y])[1];
	float t4 = dot(i.position, t3);
	float t5 = dot(i.normal.xyz, t3.xyz);
	float4 t6 = transpose(bones[D3DCOLORtoUBYTE4(i.blendindices).x])[2] * i.blendweight.x + i.blendweight.y * transpose(bones[D3DCOLORtoUBYTE4(i.blendindices).y])[2];
	float t7 = dot(i.position, t6);
	float t8 = dot(i.normal.xyz, t6.xyz);
	o.position = mul(float4(t1, t4, t7, 1), viewProj);
	o.texcoord = normalize(float3(t2, t5, t8));

	return o;
}
