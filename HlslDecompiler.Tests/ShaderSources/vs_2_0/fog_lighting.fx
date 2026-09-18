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

	float3 t0 = mul(i.normal.xyz, (float3x3)world);
	float t1 = saturate(dot(normalize(t0).xyz, -lightDirection.xyz));
	float t2 = rcp(fog.y - fog.x) * (fog.y - dot(transpose(worldViewProjection)[3], i.position));
	float t3 = rcp(dot(transpose(worldViewProjection)[3], i.position));
	o.position = mul(i.position, worldViewProjection);
	o.color = lightColour * t1 + lightColour.w;
	o.fog = saturate(t2);
	o.psize = t3 * fog.z;

	return o;
}
