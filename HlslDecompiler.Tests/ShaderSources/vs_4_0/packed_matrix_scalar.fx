int3x3 m;
bool flip;

float4 main(int3 p : P) : SV_Position
{
	int3 t0 = mul(p, m);
	return float4((float3)(flip ? -t0 : t0), 1);
}
