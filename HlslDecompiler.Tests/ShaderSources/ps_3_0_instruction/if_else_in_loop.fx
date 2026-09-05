float count;
float threshold;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float r1;
	r0 = 0;
	r1 = 0;
	for (int i0 = 0; i0 < 255; i0++) {
		if (r1.x >= count.x) {
			if (1 != -1) break;
		}
		if (threshold.x < texcoord.x) {
			r0 = r0 + texcoord;
		} else {
			r0 = r0 * 0.5;
		}
		r1 = r1.x + 1;
	}
	o = r0;

	return o;
}
