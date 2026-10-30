cbuffer c : register(b0)
{
	uint mode;
};

Buffer<float> b;
RWStructuredBuffer<float> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = sv_dispatchthreadid.x + mode & 7;
	float t1;
	if (t0 > 3) {
		t1 = t0 == 5 ? 3 : b.Load(t0).x;
	} else {
		t1 = 2;
	}
	o[sv_dispatchthreadid.x] = t1;
}
