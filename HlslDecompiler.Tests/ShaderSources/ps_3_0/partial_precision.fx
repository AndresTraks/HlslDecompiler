float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	half2 t0 = saturate(texcoord.xy);
	return float4(t0, texcoord.z, 1);
}
