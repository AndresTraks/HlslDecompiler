float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float r2;
	r0 = 0;
	r1 = 1;
	r2 = 0;
	for (int i0 = 0; i0 < 255; i0++) {
		if (r2.x >= count.x) {
			if (1 != -1) break;
		}
		r0 = r0 + texcoord;
		r1 = r1 * texcoord;
		r2 = r2.x + 1;
	}
	o = r0 + r1;

	return o;
}
