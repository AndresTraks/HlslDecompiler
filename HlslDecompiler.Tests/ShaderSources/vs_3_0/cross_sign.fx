float3 axis : register(c4);
float4 planeA : register(c5);
float4 planeB : register(c6);
float4x4 worldViewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 position : POSITION;
	float3 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.position = mul(i.position, worldViewProj);
	o.texcoord = cross(i.normal.xyz, axis);
	o.texcoord1 = float4(sign(dot(planeA, i.position)), sign(dot(planeB, i.position)), length(i.position.xyz - axis), smoothstep(0, 1, dot(planeA, i.position)));

	return o;
}
