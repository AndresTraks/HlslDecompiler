cbuffer C : register(b0)
{
	int mode;
	float4 arr[10];
};

RWStructuredBuffer<float4> o : register(u0);

[numthreads(4, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.x = ((int)sv_dispatchthreadid.x < mode) ? -1 : 0;
	r0.x = (r0.x != 0) ? 3 : 1;
	r0 = asint(arr[r0.x] + arr[r0.x]);
	o[sv_dispatchthreadid.x] = asfloat(r0);
}
