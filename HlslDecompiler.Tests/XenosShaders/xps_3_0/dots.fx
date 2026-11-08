float4 k;
float3 n;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(dot(texcoord, k), dot(texcoord.xyz, n), length(texcoord.xy), distance(texcoord.xyz, n)) + normalize(texcoord);
}
