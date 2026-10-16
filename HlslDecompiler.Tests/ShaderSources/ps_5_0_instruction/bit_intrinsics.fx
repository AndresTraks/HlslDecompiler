uint mask;
int sv;

float4 main(nointerpolation uint4 a : A) : SV_Target
{
	float4 o;

	int2 r0;
	float4 r1;
	r0.x = sv != 0 && sv != -1 ? 31 - firstbithigh(sv) : -1;
	r0.y = -(r0.x) + 31;
	r0.x = (r0.x == -1) ? -1 : 0;
	r0.y = asint((float)(uint)r0.y);
	r0.x = (r0.x != 0) ? 1333788672 : r0.y;
	r0.y = a.z != 0 ? 31 - firstbithigh(a.z) : -1;
	r0.y = -(r0.y) + 31;
	r0.y = (a.z != 0) ? r0.y : -1;
	r1.z = (float)(uint)r0.y;
	r0.y = a.x & mask;
	r0.y = countbits(r0.y);
	r1.x = (float)(uint)r0.y;
	r0.y = reversebits(a.y);
	r0.y = (uint)r0.y >> 28;
	r1.y = (float)(uint)r0.y;
	r0.y = a.w | 256;
	r0.y = firstbitlow(r0.y);
	r1.w = (float)(uint)r0.y;
	o = asfloat(r0.x) + r1;

	return o;
}
