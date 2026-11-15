float4x4 worldViewProj;
float3 axis;
float4 planeA;
float4 planeB;

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

	float t0 = dot(planeA, i.position);
	o.position = mul(i.position, worldViewProj);
	o.texcoord = cross(i.normal.xyz, axis);
	o.texcoord1 = float4(sign(t0), sign(dot(planeB, i.position)), length(i.position.xyz - axis), smoothstep(0, 1, t0));

	return o;
}
