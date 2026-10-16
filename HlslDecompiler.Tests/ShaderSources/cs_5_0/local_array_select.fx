RWStructuredBuffer<float4> o : register(u0);
RWStructuredBuffer<uint> p : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 x0[4];
	int4 x1[4];

	x0[0].x = sv_dispatchthreadid.x;
	x0[1].x = 3 * sv_dispatchthreadid.x;
	x0[2].x = sv_dispatchthreadid.x ^ 5;
	int2 t0 = sv_dispatchthreadid.x + int2(1, 2);
	x0[3].x = sv_dispatchthreadid.x + 9;
	int2 t1 = t0 & 3;
	x1[0].x = 1065353216;
	x1[1].x = 1073741824;
	x1[2].x = 1077936128;
	x1[3].x = asint((float)sv_dispatchthreadid.x);
	int t2 = sv_dispatchthreadid.x & 3;
	x0[t2].x = x1[t1.x].x;
	int t3 = x0[t1.y].x;
	p[sv_dispatchthreadid.x] = x0[t1.x].x + 1077936128;
	o[sv_dispatchthreadid.x] = float4(asfloat(x1[t1.x].x), t3 & 1 ? 1 : asfloat(x0[1].x), (float)(x0[3].x), asfloat(x1[t2].x));
}
