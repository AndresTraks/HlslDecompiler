StructuredBuffer<uint2> a : register(t0);
RWStructuredBuffer<float> o : register(u0);
RWStructuredBuffer<float2> o2 : register(u1);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.xy = a[sv_dispatchthreadid.x];
	r0.z = (uint)r0.x / (uint)r0.y;
	r0.xy = r0.xy ^ int2(-2147483648, -2147483648);
	o2[sv_dispatchthreadid.x] = asfloat(r0.xy);
	o[sv_dispatchthreadid.x] = asfloat(r0.z);
}
