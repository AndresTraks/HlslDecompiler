float4 colour;
int n;

float4 main(float color : COLOR) : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		if (color <= 0.5) {
			for (int j = 0; j < n; j++) {
				t0 = t0 + colour;
			}
		}
	}
	return t0;
}
