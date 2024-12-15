float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float r1;
	r0 = 0;
	r1.x = 3;
	for (int i0 = 0; i0 < 255; i0++) {
		r0 = r0 + texcoord;
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
