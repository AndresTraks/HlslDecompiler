uint count;
float4 k;

StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	int2 r1;
	float4 r2;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= count) ? -1 : 0;
		if (r1.y != 0) break;
		r2 = input[r1.x];
		r0 = r2 * k + r0;
		r1.x = r1.x + 1;
	}
	r1.x = max(count, 1);
	r1.x = (float)(uint)r1.x;
	r0 = r0 / r1.x;
	output[sv_dispatchthreadid.x] = r0;
}
