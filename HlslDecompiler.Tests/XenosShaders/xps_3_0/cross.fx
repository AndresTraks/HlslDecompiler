float3 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(cross(texcoord, k), 1) + float4(reflect(texcoord, k), 0);
}
