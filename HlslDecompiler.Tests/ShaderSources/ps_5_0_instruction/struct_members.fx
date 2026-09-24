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

	float4 r0;
	r0 = float4(src[sv_primitiveid.x].a, src[sv_primitiveid.x].b);
	r0 = r0.zxyw * float4(2, 2, 2, 1) + float4(0, 0, 0, 1);
	dst[sv_primitiveid.x].a = r0.xyz;
	dst[sv_primitiveid.x].b = r0.w;
	o = r0;
	r0.xy = src[sv_primitiveid.x].c;
	r0.xy = r0.yx + int2(3, 3);
	dst[sv_primitiveid.x].c = r0.xy;

	return o;
}
