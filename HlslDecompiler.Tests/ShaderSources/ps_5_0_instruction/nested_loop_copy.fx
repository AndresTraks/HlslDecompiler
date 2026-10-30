struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	float4 r2;
	int2 r3;
	r0.x = i.idx & 3;
	r0.y = ((uint)i.idx >> 2) & 3;
	r1 = float4(0, 0, 0, 0);
	r0.z = 0;
	while (true) {
		r0.w = ((uint)r0.z >= (uint)r0.x) ? -1 : 0;
		if (r0.w != 0) break;
		r2 = r1;
		r0.w = r0.z;
		r3.x = 0;
		while (true) {
			r3.y = ((uint)r3.x >= (uint)r0.y) ? -1 : 0;
			if (r3.y != 0) break;
			r3.y = (r0.w == r3.x) ? -1 : 0;
			if (r3.y != 0) {
				break;
			}
			r3.y = r0.w + r3.x;
			r3.y = asint((float)(uint)r3.y);
			r2 = i.texcoord * asfloat(r3.y) + r2;
			r3.x = r3.x + 1;
		}
		r1.xyz = r2.xyz;
		r1.w = r2.w + 1;
		r0.z = r0.z + 1;
	}
	o = r1;

	return o;
}
