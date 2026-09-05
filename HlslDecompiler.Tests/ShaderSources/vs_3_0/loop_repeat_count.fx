float4 floats[8];

float4 main() : POSITION
{
	float4 t0 = 0;
	for (int i = 0; i < 8; i++) {
		t0 = t0 + floats[i];
	}
	return t0;
}
