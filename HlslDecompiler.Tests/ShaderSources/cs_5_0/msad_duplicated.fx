cbuffer Params : register(b0)
{
	uint reference;
	uint2 source;
	uint4 mask;
};

RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint4 t0 = msad4(reference, uint2(source.x, source.y), uint4(mask.x, mask.y, mask.z, 0)).xyzx;
	output[sv_dispatchthreadid.x] = t0.wwyz;
	output[sv_dispatchthreadid.x + 64] = t0;
}
