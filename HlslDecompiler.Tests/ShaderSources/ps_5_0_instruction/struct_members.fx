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
RWStructuredBuffer<DstElement> dst : register(u1);

float4 main(nointerpolation uint sv_primitiveid : SV_PrimitiveID) : SV_Target
{
	float4 o;

	int4 r0;
	r0 = asint(float4(src[sv_primitiveid].a, src[sv_primitiveid].b));
	r0 = asint(asfloat(r0.zxyw) * float4(2, 2, 2, 1) + float4(0, 0, 0, 1));
	dst[sv_primitiveid].a = asfloat(r0.xyz);
	dst[sv_primitiveid].b = asfloat(r0.w);
	o = asfloat(r0);
	r0.xy = src[sv_primitiveid].c;
	r0.xy = r0.yx + int2(3, 3);
	dst[sv_primitiveid].c = r0.xy;

	return o;
}
