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
	int4 r0;
	r0.xyz = (uint3)source.xxx >> uint3(8, 16, 24);
	r0.yzw = (r0.xyz & ~(((1 << int3(8, 16, 24)) - 1) << int3(24, 16, 8))) | ((source.yyy << int3(24, 16, 8)) & (((1 << int3(8, 16, 24)) - 1) << int3(24, 16, 8)));
	r0.x = source.x;
	r0 = msad4(reference, uint2(r0.x, r0.w >> 8), accum);
	output[sv_dispatchthreadid.x] = r0;
}
