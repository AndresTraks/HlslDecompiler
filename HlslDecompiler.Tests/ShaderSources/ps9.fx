float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(-abs(texcoord.x * texcoord.y * texcoord.z), texcoord.x * texcoord.y * texcoord.z, 1, 2);
}
