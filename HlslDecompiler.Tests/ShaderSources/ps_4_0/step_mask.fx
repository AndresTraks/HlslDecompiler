float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return k.w * (texcoord - k) + k + (texcoord / -k.x >= -texcoord / -k.x ? frac(abs(texcoord / -k.x)) : -frac(abs(texcoord / -k.x))) * -k.x + smoothstep(k.x, k.y, texcoord) + clamp(texcoord, k.z, k.w) + (float4)sign(texcoord - k) + (texcoord >= k.y ? 1 : 0);
}
