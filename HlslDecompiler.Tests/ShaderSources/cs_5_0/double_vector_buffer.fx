StructuredBuffer<double2> input : register(t0);
RWStructuredBuffer<double2> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double t0 = input[sv_dispatchthreadid.x].y;
	double t1 = t0 * t0;
	double t2 = input[sv_dispatchthreadid.x].x;
	double t3 = input[sv_dispatchthreadid.x].y;
	output[sv_dispatchthreadid.x] = double2(t1, t3 + t2);
}
