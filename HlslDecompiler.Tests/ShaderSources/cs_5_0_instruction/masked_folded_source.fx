cbuffer Params : register(b0)
{
	uint reference;
	uint4 accum;
};

RWStructuredBuffer<uint4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	r0 = msad4(reference, uint2(int4(-235736076, -1460538637, -1482100238, -1498961679).x, int4(-235736076, -1460538637, -1482100238, -1498961679).w >> 8), accum);
	output[sv_dispatchthreadid.x] = r0;
}
