StructuredBuffer<float4> colors : register(t1);
RWStructuredBuffer<float4> tint : register(u1);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	r0 = colors[sv_dispatchthreadid.x];
	r0 = r0 * float4(2, 2, 2, 2) + float4(1, 1, 1, 1);
	tint[sv_dispatchthreadid.x] = r0;
}
