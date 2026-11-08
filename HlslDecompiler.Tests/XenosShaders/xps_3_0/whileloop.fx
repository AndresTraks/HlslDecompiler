float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	int i = 0;
	[loop]
	while (t0.x < k.x && i < 16) {
		t0 = t0 * 1.5 + 0.1;
		i++;
	}
	return t0;
}
