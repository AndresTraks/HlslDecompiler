float count;
float count2;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 t0 = 0;
	float4 t1 = 0;
	float t3 = 3;
	for (int i0 = 0; i0 < 255; i0++) {
		t0 = t0 + texcoord;
		t3.x = t3.x + 1;
	}
	o = t0;

	return o;
}
