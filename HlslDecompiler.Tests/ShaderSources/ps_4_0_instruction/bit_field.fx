uint4 packed;
float4 scale;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int2 r1;
	r0.x = (uint)asint(texcoord.x) >> 23;
	r0.x = r0.x & 255;
	r0.y = asint(texcoord.x) & 8388607;
	r0.z = r0.x << 23;
	r0.y = r0.y + r0.z;
	r0.z = (uint)texcoord.y;
	r0.z = r0.z & 3;
	r0.z = asint(dot(scale, icb[r0.z]));
	r0.w = 0;
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= 4) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = r1.x << 3;
		r1.y = (uint)packed.x >> r1.y;
		r1.y = r1.y & 255;
		r1.y = (128 < r1.y) ? -1 : 0;
		r0.w = r0.w + -(r1.y);
		r1.x = r1.x + 1;
	}
	o.x = asfloat(r0.z) * asfloat(r0.y);
	o.yz = (float2)(uint2)r0.wx;
	o.w = 0;

	return o;
}
