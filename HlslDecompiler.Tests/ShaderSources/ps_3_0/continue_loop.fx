float4 colour;
int n;

float4 main(float color : COLOR) : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		if (color > 0.5) {
		} else {
			float4 t1 = t0;
			for (int j = 0; j < n; j++) {
				t1 = t1 + colour;
			}
			t0 = t1;
		}
	}
	return t0;
}
