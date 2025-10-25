float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	return float4(texcoord.yzx, 3);
}
