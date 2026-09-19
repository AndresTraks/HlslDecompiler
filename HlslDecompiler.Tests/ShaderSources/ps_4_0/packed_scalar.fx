float3 lightDir;
float3 viewDir;
float power;
float4 diffuse;
float4 spec;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float t0 = saturate(dot(normalize(normal), -lightDir));
	float t1 = pow(saturate(dot(normalize(normal), normalize(viewDir - lightDir))), power);
	return diffuse * t0 + t1 * spec;
}
