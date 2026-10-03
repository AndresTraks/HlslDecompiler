float3 lightDir;
float3 viewDir;
float power;
float4 diffuse;
float4 spec;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float3 t0 = normalize(viewDir - lightDir);
	float3 t1 = normalize(normal);
	float t2 = saturate(dot(t1, -lightDir));
	float t3 = pow(saturate(dot(t1, t0)), power);
	return diffuse * t2 + t3 * spec;
}
