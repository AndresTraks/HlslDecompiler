float4 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = saturate(texcoord) * k.x;
	return float4(t0 * (half3)(t0 * k.y + t0), 1);
}
