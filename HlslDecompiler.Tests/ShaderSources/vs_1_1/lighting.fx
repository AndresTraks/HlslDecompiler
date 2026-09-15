float4 ambient : register(c3);
float3 halfVector : register(c1);
float3 lightDir;
float specularPower : register(c2);

float4 main(float4 normal : NORMAL) : POSITION
{
	float t0 = dot(halfVector, normal.xyz);
	float t1 = dot(lightDir, normal.xyz);
	return ambient * lit(t1, t0, specularPower).y + lit(t1, t0, specularPower).z;
}
