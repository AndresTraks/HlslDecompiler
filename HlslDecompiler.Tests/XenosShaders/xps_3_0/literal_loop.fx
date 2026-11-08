float4 a[6];

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	for (int i = 0; i < 6; i++) {
		t0 = t0 * a[i] + float4(0.1, 0.2, 0.3, 0.4) * i + float4(1.1, 1.2, 1.3, 1.4);
	}
	return t0 + float4(5, 6, 7, 8);
}
