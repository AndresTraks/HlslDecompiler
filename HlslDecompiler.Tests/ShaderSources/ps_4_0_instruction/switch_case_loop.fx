uint4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int3 r1;
	float4 r2;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = ((uint)r1.x >= k.x) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = r1.x & 1;
		switch (r1.y) {
			case 0:
			r2 = r0 + texcoord;
			break;
			default:
			r2 = r0;
			r1.y = 0;
			while (true) {
				r1.z = ((uint)r1.y >= k.y) ? -1 : 0;
				if (r1.z != 0) break;
				r1.z = asint((float)(uint)r1.y);
				r2 = texcoord * asfloat(r1.z) + r2;
				r1.y = r1.y + 1;
			}
			break;
		}
		r1.y = (50 < r2.y) ? -1 : 0;
		if (r1.y != 0) {
			r0 = r2;
			break;
		}
		r0 = r2;
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
