uint count;
float4 k;

StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = 0;
	for (int t1 = 0; t1 < count; t1 = t1 + 1) {
		t0 = input[t1] * k + t0;
	}
	int t2 = (float)(max(count, 1));
	output[sv_dispatchthreadid.x] = t0 / t2;
}
