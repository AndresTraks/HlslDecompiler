float4 fog : register(c9);
float4 lightColour : register(c8);
float4 lightDirection : register(c7);
float4x4 world : register(c4);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 color : COLOR;
	float fog : FOG;
	float4 position : POSITION;
	float psize : PSIZE;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = saturate(dot(normalize(mul(i.normal.xyz, (float3x3)world)), -lightDirection.xyz));
	float4 t1 = mul(i.position, worldViewProjection);
	float t2 = (fog.y - t1.w) / (fog.y - fog.x);
	o.position = t1;
	o.color = lightColour * t0 + lightColour.w;
	o.fog = saturate(t2);
	o.psize = fog.z / t1.w;

	return o;
}
