struct S
{
	float4 x;
	float4 y;
	float4 z;
};

cbuffer C : register(b0)
{
	uint mode;
	S arr[6];
};

RWStructuredBuffer<float4> o : register(u0);

[numthreads(4, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = mode | 1;
	uint t1 = sv_dispatchthreadid.x >> 1;
	int t2 = t0 + 1;
	o[sv_dispatchthreadid.x] = arr[t1 + 1].y + arr[(mode & 1) + 2].z + arr[t2].x;
}
