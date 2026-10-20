cbuffer Splits : register(b0)
{
	float4 splits;
	float depth;
};

RWStructuredBuffer<int> cascade : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = (float)sv_dispatchthreadid.x + depth;
	int t1 = 0;
	for (int t2 = 0; t2 < 4; t2 = t2 + 1) {
		t1 = splits[t2] < t0 ? t2 + 1 : t1;
	}
	cascade[sv_dispatchthreadid.x] = t1;
}
