cbuffer Params : register(b0)
{
	uint reference;
	uint2 source;
	uint4 accum;
};

RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint4 t0 = msad4(reference, uint2(source.x, source.y), accum);
	output[sv_dispatchthreadid.x] = msad4(reference + 1, uint2(source.x, source.y), t0);
}
