float4 arr[8];
int n;

float4 main() : SV_Position
{
	float4 t0 = 0;
	for (int t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = t0 + arr[t1 & 7];
	}
	return t0;
}
