float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	return float4(-abs(texcoord.x * texcoord.y * texcoord.z), texcoord.x * texcoord.y * texcoord.z, 1, 2);
}
