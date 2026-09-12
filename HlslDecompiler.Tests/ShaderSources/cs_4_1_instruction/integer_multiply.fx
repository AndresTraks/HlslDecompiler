uint n;
uint stride;

RWStructuredBuffer<uint> data : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.x = sv_dispatchthreadid.x * stride;
	r0.y = sv_dispatchthreadid.x * stride + stride;
	r0.z = (r0.y < n) ? -1 : 0;
	if (r0.z != 0) {
		r0.z = data[r0.y];
		r0.x = data[r0.x];
		r0.x = r0.x + r0.z;
		data[r0.y] = r0.x;
	}
}
