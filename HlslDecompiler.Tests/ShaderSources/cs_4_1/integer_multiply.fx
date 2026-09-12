uint n;
uint stride;

RWStructuredBuffer<uint> data : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 t0 = int2(sv_dispatchthreadid.x * stride, sv_dispatchthreadid.x * stride + stride);
	int t1;
	if (t0.y < n) {
		t1 = data[t0.y];
		data[sv_dispatchthreadid.x * stride + stride] = data[t0.x] + t1;
	}
}
