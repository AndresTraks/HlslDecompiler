cbuffer Params : register(b0)
{
	uint offset;
};

StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 x0[8];

	x0[0].x = input[sv_dispatchthreadid.x];
	int3 t0 = sv_dispatchthreadid.x + int3(2, 3, 4);
	x0[1].x = input[sv_dispatchthreadid.x + 1];
	x0[2].x = input[t0.x];
	float t1 = input[t0.z];
	x0[3].x = input[t0.y];
	x0[4].x = t1;
	int t2 = sv_dispatchthreadid.x + 6;
	t0.y = sv_dispatchthreadid.x + 7;
	x0[5].x = input[sv_dispatchthreadid.x + 5];
	float t3 = input[t0.y];
	x0[6].x = input[t2];
	x0[7].x = t3;
	t3 = x0[(offset & 5) + 2].x + x0[(offset & 5) + 1].x;
	output[sv_dispatchthreadid.x] = 0.25 * t3 + 0.5 * x0[offset & 5].x;
}
