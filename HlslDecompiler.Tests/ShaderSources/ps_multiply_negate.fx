float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(-abs(texcoord.y * texcoord.x * texcoord.z), texcoord.y * texcoord.x * texcoord.z, 1, 2);
}
