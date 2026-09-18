float4 palette[4];
uint stride;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int3 r1;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= 4) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = r1.x * stride;
		r1.y = r1.y & 3;
		r1.z = asint((float)(uint)r1.x);
		r1.z = asint(asfloat(r1.z) + texcoord.x);
		r0 = palette[r1.y] * asfloat(r1.z) + r0;
		r1.x = r1.x + 1;
	}
	r1.x = (uint)asint(texcoord.y) >> 23;
	r1.x = r1.x & 255;
	r1.x = asint((float)(uint)r1.x);
	o = r0 + asfloat(r1.x);

	return o;
}
