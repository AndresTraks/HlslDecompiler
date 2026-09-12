float4 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(sin(k.x * texcoord.x) * k.x, cos(k.x * texcoord.x) * k.y, sin(texcoord.y) * k.z, cos(texcoord.z) * k.w);
}
