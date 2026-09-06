float4 ambient : register(c3);
float3 halfVector : register(c1);
float3 lightDir;
float specularPower : register(c2);

float4 main(float4 normal : NORMAL) : POSITION
{
	return ambient * lit(dot(lightDir, normal.xyz), dot(halfVector, normal.xyz), specularPower).y + lit(dot(lightDir, normal.xyz), dot(halfVector, normal.xyz), specularPower).z;
}
