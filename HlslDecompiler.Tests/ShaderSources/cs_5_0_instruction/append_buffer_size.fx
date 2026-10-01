AppendStructuredBuffer<float4> appended : register(u0);
RWStructuredBuffer<float> output : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int r0;
	uint2 dimensions0;
	appended.GetDimensions(dimensions0.x, dimensions0.y);
	r0 = dimensions0.x;
	r0 = r0.x + 16;
	r0 = asint((float)(uint)r0.x);
	output[sv_dispatchthreadid.x] = asfloat(r0.x);
}
