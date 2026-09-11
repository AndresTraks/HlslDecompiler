float3 lightDir;
float3 viewDir;
float power;
float4 diffuse;
float4 spec;

float4 main(float3 normal : NORMAL) : SV_Target
{
	return diffuse * saturate(dot(normalize(normal), -lightDir)) + exp2(log2(saturate(dot(normalize(normal), normalize(viewDir - lightDir)))) * power) * spec;
}
