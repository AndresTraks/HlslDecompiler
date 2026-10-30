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
	int4 r0;
	int r1;
	r0.x = (uint)sv_dispatchthreadid.x >> 1;
	r0.y = mode & 1;
	r0.xy = r0.xy * int2(3, 3);
	r0 = asint(arr[r0.x / 3 + 1].y + arr[r0.y / 3 + 2].z);
	r1 = mode | 1;
	r1 = r1.x * 3 + 3;
	r0 = asint(asfloat(r0) + arr[r1.x / 3].x);
	o[sv_dispatchthreadid.x] = asfloat(r0);
}
