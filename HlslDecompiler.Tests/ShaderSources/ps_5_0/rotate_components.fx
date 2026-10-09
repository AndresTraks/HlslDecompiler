float4 v;
int n;

float4 main() : SV_Target
{
	float3 t0 = v.xyz;
	for (int t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = 1.5 * t0.yzx;
	}
	return float4(t0, v.w);
}
