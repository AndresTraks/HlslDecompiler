float4 v;
int n;

float4 main() : SV_Target
{
	float3 t0 = v.xyz;
	for (int t1 = 0; t1 < n; t1 = t1 + 1) {
		float t2 = 1.5 * t0.x;
		t0.x = 1.5 * t0.y;
		t0.y = 1.5 * t0.z;
		t0.z = t2;
	}
	return float4(t0, v.w);
}
