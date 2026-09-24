float4 v;
int n;

float4 main() : SV_Target
{
	float4 t0 = v;
	float4 t1 = v.wzyx;
	for (int t2 = 0; t2 < n; t2 = t2 + 1) {
		float t3 = t0.w + 0.25;
		float t4 = t0.z + 0.25;
		float t5 = t0.y + 0.25;
		float t6 = t0.x + 0.25;
		t0 = 0.5 * t1;
		t1 = float4(t6, t5, t4, t3);
	}
	return 2 * t1 + t0;
}
