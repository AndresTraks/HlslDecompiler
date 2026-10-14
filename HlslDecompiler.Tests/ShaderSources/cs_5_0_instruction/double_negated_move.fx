StructuredBuffer<double2> input : register(t0);
RWStructuredBuffer<double2> output : register(u0);
RWStructuredBuffer<int2> flags : register(u1);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	double2 d0;
	int4 r1;
	double2 d1;
	double2 d2;
	d0 = input[sv_dispatchthreadid.x];
	r1.x = asint((-(d0.x) < d0.y) ? -1 : 0);
	d1.x = (asfloat(r1.x) != 0) ? -(d0.x) : d0.y;
	d1.y = min(d0.x, d1.x);
	d1.y = -(d1.y);
	d2.y = d1.y;
	d1.y = max(-(d0.x), d1.x);
	r1.x = asint((d0.x != d1.x) ? -1 : 0);
	r0.x = asint((d0.y == d0.x) ? -1 : 0);
	r0.x = r0.x & 1;
	r0.y = r1.x & 1;
	flags[sv_dispatchthreadid.x] = r0.xy;
	d2.x = d1.y;
	output[sv_dispatchthreadid.x] = d2;
}
