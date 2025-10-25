float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	return float4(-abs(texcoord.z), texcoord.x, 1, 2);
}
