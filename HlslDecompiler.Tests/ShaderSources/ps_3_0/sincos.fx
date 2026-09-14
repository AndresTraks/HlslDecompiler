float4 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(sin(k.x * texcoord.x), cos(k.x * texcoord.x), sin(texcoord.y), cos(texcoord.z)) * k;
}
