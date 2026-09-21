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
	float t2 = rcp(fog.y - fog.x) * (fog.y - t1.w);
	float t3 = rcp(t1.w);
	o.position = t1;
	o.color = lightColour * t0 + lightColour.w;
	o.fog = saturate(t2);
	o.psize = t3 * fog.z;

	return o;
}
