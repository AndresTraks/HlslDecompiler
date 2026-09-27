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
	int4 r1;
	float4 r2;
	r0.x = reference + 1;
	r0.yzw = (uint3)source.xxx >> uint3(8, 16, 24);
	r1.yzw = (r0.yzw & ~(((1 << int3(8, 16, 24)) - 1) << int3(24, 16, 8))) | ((source.yyy << int3(24, 16, 8)) & (((1 << int3(8, 16, 24)) - 1) << int3(24, 16, 8)));
	r1.x = source.x;
	r2 = msad4(reference, uint2(r1.x, r1.w >> 8), accum);
	r0 = msad4(r0.x, uint2(r1.x, r1.w >> 8), (int4)r2);
	output[sv_dispatchthreadid.x] = r0;
}
