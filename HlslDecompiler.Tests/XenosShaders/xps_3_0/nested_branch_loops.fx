bool b : register(b0);
float4 k[4];
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	[branch]
	if (b) {
		[loop]
		for (int i = 0; i < n; i++) {
			if (((k[i].x < t0.x) ? 1 : 0) != 0) {
				t0 = t0 - k[i];
			} else {
				t0 = t0 + 0.5 * k[i];
			}
		}
	}
	return t0;
}
