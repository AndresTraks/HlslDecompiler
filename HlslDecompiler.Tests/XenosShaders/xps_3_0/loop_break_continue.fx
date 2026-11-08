float4 k[8];
int n;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		if (k[i].w < 0) {
			continue;
		}
		t0 = t0 + texcoord * k[i];
		if (t0.x > 10) {
			break;
		}
	}
	return t0;
}
