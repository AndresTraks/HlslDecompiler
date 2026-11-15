float4x4 worldViewProjection;
float4x4 world;
float4 lightDirection;
float4 lightColour;
float4 fog;

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

	float3 t0 = normalize(mul(i.normal.xyz, (float3x3)world));
	float t1 = saturate(dot(t0, -lightDirection.xyz));
	float4 t2 = mul(i.position, worldViewProjection);
	float t3 = (fog.y - t2.w) / (fog.y - fog.x);
	o.color = lightColour * t1 + lightColour.w;
	o.fog = saturate(t3);
	o.position = t2;
	o.psize = fog.z / t2.w;

	return o;
}
