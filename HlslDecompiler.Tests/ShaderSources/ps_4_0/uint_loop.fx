uint n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	for (float t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = texcoord * (t1 * 4) + t0;
	}
	return t0;
}
