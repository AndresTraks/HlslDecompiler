RWTexture2D<unorm float4> img : register(u0);
RWBuffer<int2> ib : register(u1);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 t0 = ib[sv_dispatchthreadid.x].xy & 1065353216;
	float2 t1 = img[sv_dispatchthreadid.xy].yz;
	img[sv_dispatchthreadid.xy] = img[sv_dispatchthreadid.xy].x > 0.5 ? asfloat(int4(t0, ib[sv_dispatchthreadid.x].xy & 1065353216)) : float4(img[sv_dispatchthreadid.xy].x, t1, img[sv_dispatchthreadid.xy].w) * (float4)(ib[sv_dispatchthreadid.x].x);
	ib[sv_dispatchthreadid.x] = uint2(asuint(t1.x), (int)t1.y);
}
