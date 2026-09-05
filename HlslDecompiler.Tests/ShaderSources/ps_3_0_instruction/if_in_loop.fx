float count;
float threshold;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float2 r0;
	float4 r1;
	float4 r2;
	r0.x = threshold.x + -texcoord.x;
	r1 = 0;
	r0.y = 0;
	for (int i0 = 0; i0 < 255; i0++) {
		if (r0.y >= count.x) {
			if (1 != -1) break;
		}
		r2 = r1 + texcoord;
		r1 = (r0.x >= 0) ? r1 : r2;
		r0.y = r0.y + 1;
	}
	o = r1;

	return o;
}
