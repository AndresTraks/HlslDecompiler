cbuffer C : register(b0)
{
	int mode;
	float4 arr[10];
};

RWStructuredBuffer<float4> o : register(u0);

[numthreads(4, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = (int)sv_dispatchthreadid.x < mode ? 3 : 1;
	o[sv_dispatchthreadid.x] = arr[t0] + arr[t0];
}
