float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	bool t0 = any(k.xyz >= texcoord.xyz) && any(k.xyz < texcoord.xyz);
	float3 t1 = max(texcoord.xyz, k.xyz);
	return float4(t0 ? 2 * t1 : t1, isinf(texcoord.w) ? 1 : texcoord.w);
}
