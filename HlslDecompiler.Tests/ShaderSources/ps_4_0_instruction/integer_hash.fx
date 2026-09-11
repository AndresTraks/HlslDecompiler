uint seed;
uint count;

float4 main() : SV_Target
{
	float4 o;

	int4 r0;
	r0.x = seed;
	r0.y = 0;
	while (true) {
		r0.z = (r0.y >= count) ? -1 : 0;
		if (r0.z != 0) break;
		r0.z = r0.x * 1664525 + 1013904223;
		r0.w = r0.x >> 16;
		r0.x = r0.w ^ r0.z;
		r0.y = r0.y + 1;
	}
	r0.y = r0.x & 255;
	r0.y = r0.y;
	o.x = r0.y * 0.00392156886;
	r0.y = r0.x >> 8;
	r0.z = r0.x >> 16;
	r0.xy = r0.yz & int2(255, 255);
	r0.xy = r0.xy;
	o.yz = r0.xy * float2(0, 0.00392156886);
	o.w = 1;

	return o;
}
