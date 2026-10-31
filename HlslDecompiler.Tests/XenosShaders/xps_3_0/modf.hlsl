float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 1.42857146 * texcoord;
	return 0.699999988 * (t0 >= 0 ? frac(abs(t0)) : -frac(abs(t0))) + texcoord;
}
