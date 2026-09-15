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

	float t0 = dot(planeA, i.position);
	float t1 = dot(planeB, i.position);
	float t2 = saturate(t0);
	o.position = mul(i.position, worldViewProj);
	o.texcoord = i.normal.yzx * axis.zxy - axis.yzx * i.normal.zxy;
	o.texcoord1 = float4(((-t0 < t0) ? 1 : 0) - ((t0 < -t0) ? 1 : 0), ((-t1 < t1) ? 1 : 0) - ((t1 < -t1) ? 1 : 0), length(i.position.xyz - axis), t2 * t2 * (-2 * t2 + 3));

	return o;
}
