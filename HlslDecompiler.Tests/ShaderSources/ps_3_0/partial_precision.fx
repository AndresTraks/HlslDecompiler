half4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4((half2)saturate(texcoord.xy), texcoord.z, 1);
}
