float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0;
	float4 t1 = modf(texcoord, t0);
	return t1 + t0 + fmod(texcoord, 0.7);
}
