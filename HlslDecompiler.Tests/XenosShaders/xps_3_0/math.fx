float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(pow(abs(texcoord.x), texcoord.y), log2(abs(texcoord.z)), exp2(texcoord.w), rsqrt(abs(texcoord.x))) + frac(texcoord) + floor(texcoord) + sign(texcoord) + abs(texcoord);
}
