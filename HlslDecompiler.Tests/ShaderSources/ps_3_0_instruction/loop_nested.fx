float count;
float count2;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float2 r2;
	float4 r3;
	r0 = 0;
	r1 = 0;
	r2.x = 3;
	for (int i0 = 0; i0 < 255; i0++) {
		if (r2.x >= count.x) {
			if (1 != -1) break;
		}
		r3 = r1;
		r2.y = 5;
		for (int i1 = 0; i1 < 255; i1++) {
			if (r2.y >= count2.x) {
				if (1 != -1) break;
			}
			r3 = r3 + texcoord;
			r2.y = r2.y + 1;
		}
		r1 = r3;
		r0 = r0 + texcoord;
		r2.x = r2.x + 1;
	}
	o = r0 + r1;

	return o;
}
