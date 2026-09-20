float4 colour;
int n;

float4 main() : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		if (t0.w > 5) {
		} else {
			float4 t1 = t0.wxyz;
			for (int j = 0; j < n; j++) {
				float t2 = 3 - t1.y;
				t1.xzw = t2 >= 0 ? t1.xzw + colour.xzw : t1.xzw;
				t1.y = t2 >= 0 ? t1.y + colour.y : t1.y;
			}
			t0 = t1.yzwx;
		}
	}
	return t0.wxyz;
}
