float4 m[4];
int n;

float4 main() : POSITION
{
	float t0 = (0 < n) ? 1 : 0;
	float t1 = (t0 < n) ? 1 : 0;
	float t4 = lerp(t0, 2, t1);
	float t2 = ((t4 < n) ? 1 : 0) * t1;
	float t3 = ((lerp(t4, 3, t2) < n) ? 1 : 0) * t2;
	float4 t5 = 0;
	for (int i = 0; i < n; i++) {
		t5 = t3 * m[3] + t2 * m[2] + t1 * m[1] + t0 * m[0] + t5;
	}
	return t5;
}
