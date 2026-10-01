AppendStructuredBuffer<float4> appended : register(u0);
RWStructuredBuffer<float> output : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint2 t0;
	appended.GetDimensions(t0.x, t0.y);
	output[sv_dispatchthreadid.x] = (float)(t0.x + 16);
}
