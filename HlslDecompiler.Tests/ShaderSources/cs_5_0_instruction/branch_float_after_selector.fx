cbuffer c : register(b0)
{
	uint mode;
};

Buffer<float> b;
RWStructuredBuffer<float> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = sv_dispatchthreadid.x + mode;
	r0.x = r0.x & 7;
	r0.y = (3 < (uint)r0.x) ? -1 : 0;
	if (r0.y != 0) {
		r0.y = asint(b.Load(r0.x).x);
		r0.x = (r0.x == 5) ? -1 : 0;
		r0.x = (r0.x != 0) ? 1077936128 : r0.y;
	} else {
		r0.x = 1073741824;
	}
	o[sv_dispatchthreadid.x] = asfloat(r0.x);
}
