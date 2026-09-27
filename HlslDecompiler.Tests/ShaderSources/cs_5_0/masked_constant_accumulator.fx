cbuffer Params : register(b0)
{
	uint reference;
	uint2 source;
};

RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x] = msad4(reference, uint2(source.x, source.y), uint4(1, 2, 3, 4));
}
