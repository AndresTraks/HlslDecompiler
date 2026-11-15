float3 lightDir;
float3 halfVector;
float specularPower;
float4 ambient;

float4 main(float4 normal : NORMAL) : POSITION
{
	float t0 = dot(lightDir, normal.xyz);
	float t1 = dot(halfVector, normal.xyz);
	float2 t2 = lit(t0, t1, specularPower).yz;
	return ambient * t2.x + t2.y;
}
