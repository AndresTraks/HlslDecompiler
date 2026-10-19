struct SrcElement
{
	float3 a;
	float b;
	uint2 c;
};

struct DstElement
{
	float3 a;
	float b;
	uint2 c;
};

StructuredBuffer<SrcElement> src : register(t0);
RWStructuredBuffer<DstElement> dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	int3 r1;
	r0 = int3(asint(src[sv_dispatchthreadid.x].b), src[sv_dispatchthreadid.x].c);
	r0.yz = asint((float2)(uint2)r0.zy);
	r1.x = asint(asfloat(r0.x) + asfloat(r0.x));
	r0.xy = asint(asfloat(r0.yz) + float2(3, 3));
	r1.yz = (uint2)asfloat(r0.xy);
	dst[sv_dispatchthreadid.x].b = asfloat(r1.x);
	dst[sv_dispatchthreadid.x].c = r1.yz;
}
