float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return smoothstep(k.x, k.y, texcoord) + clamp(texcoord, k.z, k.w) + (float4)sign(texcoord - k) + fmod(texcoord, -k.x) + (texcoord >= k.y ? 1 : 0) + lerp(k, texcoord, k.w);
}
