float4 main(float2 texcoord : TEXCOORD) : SV_TARGET
{
	double t0 = (double)texcoord.x * 2 + 1.5;
	double2 t1 = (double2)texcoord;
	return float4((float2)(double2(t1.x * 0.25 + 1.5, t1.y * 3 + 1.5)) + (float)t0, 4 < t0, 0);
}
