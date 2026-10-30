cbuffer c : register(b0)
{
	float scale;
	uint n;
};

RWStructuredBuffer<uint> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int2 r1;
	r0.x = asint((float)(uint)sv_dispatchthreadid.x);
	r0.x = asint(asfloat(r0.x) * scale);
	r0.y = n & 15;
	r0.z = r0.x;
	r0.w = 0;
	r1.x = 0;
	while (true) {
		r1.y = ((uint)r1.x >= (uint)r0.y) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = asint(asfloat(r0.z) + asfloat(r0.z));
		r0.z = asint(frac(asfloat(r1.y)));
		r1.y = (asfloat(r0.z) >= 0.5) ? -1 : 0;
		r0.w = (r1.y & ~(2147483647 << 1)) | ((r0.w << 1) & (2147483647 << 1));
		r1.x = r1.x + 1;
	}
	o[sv_dispatchthreadid.x] = r0.w;
}
