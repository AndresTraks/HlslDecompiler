cbuffer Params : register(b0)
{
	uint reference;
	uint4 accum;
};

StructuredBuffer<uint2> words : register(t0);
RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x] = msad4(reference, uint2(words[sv_dispatchthreadid.x].x, words[sv_dispatchthreadid.x].y), accum);
}
