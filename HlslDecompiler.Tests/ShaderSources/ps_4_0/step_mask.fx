float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = texcoord / -k.x;
	return k.w * (texcoord - k) + k + (t0 >= -t0 ? frac(abs(t0)) : -frac(abs(t0))) * -k.x + smoothstep(k.x, k.y, texcoord) + clamp(texcoord, k.z, k.w) + (float4)sign(texcoord - k) + (texcoord >= k.y ? 1 : 0);
}
