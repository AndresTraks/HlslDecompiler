float4x4 m;
float3x3 n;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(mul(texcoord, (float4x2)m) + mul(texcoord.xyz, (float3x2)n), dot(transpose(n)[2], texcoord.xyz) + dot(transpose(m)[2], texcoord), dot(transpose(m)[3], texcoord));
}
