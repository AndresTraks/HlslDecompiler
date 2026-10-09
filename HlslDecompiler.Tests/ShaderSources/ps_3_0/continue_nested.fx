float4 colour;
int n;

float4 main() : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		if (t0.w <= 5) {
			for (int j = 0; j < n; j++) {
				t0.wxyz = 3 - t0.x >= 0 ? t0.wxyz + colour : t0.wxyz;
			}
		}
	}
	return t0.wxyz;
}
