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
	int4 r0;
	int r1;
	r0.xy = (uint2)source.xx >> uint2(8, 16);
	r0.yz = (r0.xy & ~(((1 << int2(8, 16)) - 1) << int2(24, 16))) | ((source.yy << int2(24, 16)) & (((1 << int2(8, 16)) - 1) << int2(24, 16)));
	r0.xw = source.xx;
	r0 = uint4(msad4(reference, uint2(r0.x, 0), uint4(mask.x, 0, 0, 0)).x, msad4(reference, uint2(r0.y, 0), uint4(mask.y, 0, 0, 0)).x, msad4(reference, uint2(r0.z, 0), uint4(mask.z, 0, 0, 0)).x, msad4(reference, uint2(r0.w, 0), uint4(mask.x, 0, 0, 0)).x);
	output[sv_dispatchthreadid.x] = r0.wwyz;
	r1 = sv_dispatchthreadid.x + 64;
	output[r1.x] = r0;
}
