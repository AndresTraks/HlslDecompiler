RWTexture2D<unorm float4> img : register(u0);
RWBuffer<int2> ib : register(u1);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int4 r1;
	int4 r2;
	r0 = asint(img[sv_dispatchthreadid.xy]);
	r1.xy = ib[sv_dispatchthreadid.x].xy;
	r2 = r1.xyxy & int4(1065353216, 1065353216, 1065353216, 1065353216);
	r1.x = asint((float)r1.x);
	r1 = asint(asfloat(r0) * asfloat(r1.x));
	r0.x = (0.5 < asfloat(r0.x)) ? -1 : 0;
	r1 = (r0.x != 0) ? r2 : r1;
	img[sv_dispatchthreadid.xy] = asfloat(r1);
	r1.y = (int)asfloat(r0.z);
	r1.xzw = r0.yyy;
	ib[sv_dispatchthreadid.x] = r1;
}
