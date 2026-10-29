cbuffer C : register(b0)
{
	int mode;
};

RWStructuredBuffer<float> outbuf : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.xy = int2(0, 0);
	while (true) {
		r0.z = (r0.y >= 8) ? -1 : 0;
		if (r0.z != 0) break;
		switch (mode) {
			case 0:
			r0.w = asint((float)r0.y);
			r0.z = asint(asfloat(r0.w) + asfloat(r0.x));
			break;
			case 1:
			r0.z = asint(asfloat(r0.x) + 100);
			break;
			case 2:
			r0.w = (4 < r0.y) ? -1 : 0;
			if (r0.w != 0) {
				r0.z = r0.x;
				break;
			}
			r0.z = asint(asfloat(r0.x) + 200);
			break;
			default:
			r0.z = r0.x;
			break;
		}
		r0.w = r0.y & 1;
		if (r0.w != 0) {
			r0.w = r0.y + 1;
			r0.x = r0.z;
			r0.y = r0.w;
			continue;
		}
		r0.y = r0.y + 1;
		r0.xy = r0.zy;
	}
	outbuf[sv_dispatchthreadid.x] = asfloat(r0.x);
}
