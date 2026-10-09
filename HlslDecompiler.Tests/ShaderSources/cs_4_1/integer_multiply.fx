cbuffer cb : register(b0)
{
	uint n;
	uint stride;
};

RWStructuredBuffer<uint> data : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = sv_dispatchthreadid.x * stride;
	uint2 t1 = uint2(t0, t0 + stride);
	if (t1.y < n) {
		uint t2 = data[t1.y];
		data[t1.y] = t2 + data[t1.x];
	}
}
