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
	uint t0 = words[sv_dispatchthreadid.x].y;
	uint t1 = words[sv_dispatchthreadid.x].x;
	output[sv_dispatchthreadid.x] = msad4(reference, uint2(t1, t0), accum);
}
