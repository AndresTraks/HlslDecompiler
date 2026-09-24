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
	float4 t0 = float4(2 * src[sv_primitiveid].a.zxy, src[sv_primitiveid].b + 1);
	dst[sv_primitiveid].a = t0.xyz;
	dst[sv_primitiveid].b = t0.w;
	dst[sv_primitiveid].c = src[sv_primitiveid].c.yx + 3;
	return t0;
}
