float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(texcoord.yzx, 3);
}
