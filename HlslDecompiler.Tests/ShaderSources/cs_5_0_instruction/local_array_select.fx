RWStructuredBuffer<float4> o : register(u0);
RWStructuredBuffer<uint> p : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	int4 r1;
	int4 x0[4];
	int4 x1[4];
	x0[0].x = sv_dispatchthreadid.x;
	r0.x = sv_dispatchthreadid.x * 3;
	x0[1].x = r0.x;
	r0.x = sv_dispatchthreadid.x ^ 5;
	x0[2].x = r0.x;
	r0 = sv_dispatchthreadid.xxx + int3(9, 1, 2);
	x0[3].x = r0.x;
	r0.xy = r0.yz & int2(3, 3);
	x1[0].x = 1065353216;
	x1[1].x = 1073741824;
	x1[2].x = 1077936128;
	r0.z = asint((float)(uint)sv_dispatchthreadid.x);
	x1[3].x = r0.z;
	r1.x = x1[r0.x].x;
	r0.z = sv_dispatchthreadid.x & 3;
	x0[r0.z].x = r1.x;
	r1.w = x1[r0.z].x;
	r0.y = x0[r0.y].x;
	r0.x = x0[r0.x].x;
	r0.x = r0.x + 1077936128;
	p[sv_dispatchthreadid.x] = r0.x;
	r0.x = r0.y & 1;
	r0.y = x0[1].x;
	r0.z = x0[3].x;
	r1.z = asint((float)r0.z);
	r1.y = (r0.x != 0) ? 1065353216 : r0.y;
	o[sv_dispatchthreadid.x] = asfloat(r1);
}
