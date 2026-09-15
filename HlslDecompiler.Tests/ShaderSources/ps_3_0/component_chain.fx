float4 c;
int n;

float4 main() : COLOR
{
	float3 t0 = c.xyw;
	for (int i = 0; i < n; i++) {
		t0.y = 2 * t0.y + 1;
		t0.x = t0.y + c.z;
		t0.z = t0.x - c.w;
	}
	return float4(t0.xy, c.z, t0.z);
}
