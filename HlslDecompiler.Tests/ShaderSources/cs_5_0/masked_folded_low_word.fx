cbuffer Params : register(b0)
{
	uint reference;
	uint high;
	uint4 accum;
};

RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x] = msad4(reference, uint2(16909060, high), accum);
}
