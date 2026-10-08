float4 colour;
int n;

float4 main() : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		if (t0.w <= 5) {
			for (int j = 0; j < n; j++) {
				float t1 = 3 - t0.x;
				t0.wyz = t1 >= 0 ? t0.wyz + colour.xzw : t0.wyz;
				t0.x = t1 >= 0 ? t0.x + colour.y : t0.x;
			}
		}
	}
	return t0.wxyz;
}
