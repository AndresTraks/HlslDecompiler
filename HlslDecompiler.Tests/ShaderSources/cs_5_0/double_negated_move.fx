StructuredBuffer<double2> input : register(t0);
RWStructuredBuffer<double2> output : register(u0);
RWStructuredBuffer<int2> flags : register(u1);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double2 t1 = input[sv_dispatchthreadid.x];
	double t0 = t1.y;
	double t2 = max(-t1.x, -t1.x < t0 ? -t1.x : t0);
	flags[sv_dispatchthreadid.x] = int2(t0 == t1.x ? 1 : 0, t1.x != (-t1.x < t0 ? -t1.x : t0) ? 1 : 0);
	output[sv_dispatchthreadid.x] = double2(t2, -min(t1.x, -t1.x < t0 ? -t1.x : t0));
}
