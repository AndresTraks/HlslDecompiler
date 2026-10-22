cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<float> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = s[sv_dispatchthreadid.x];
	float t1 = (float)(int)t0 + (float)(int)(sv_dispatchthreadid.x - 4) + (float)t0 + (float)(int)(n - 9);
	o[sv_dispatchthreadid.x] = (float)(sv_dispatchthreadid.x - 4) + t1;
}
