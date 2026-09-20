float4 ambient : register(c3);
float3 halfVector : register(c1);
float3 lightDir;
float specularPower : register(c2);

float4 main(float4 normal : NORMAL) : POSITION
{
	float t0 = dot(lightDir, normal.xyz);
	float t1 = dot(halfVector, normal.xyz);
	float2 t2 = lit(t0, t1, specularPower).yz;
	return ambient * t2.x + t2.y;
}
