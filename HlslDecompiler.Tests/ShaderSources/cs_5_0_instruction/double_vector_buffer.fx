StructuredBuffer<double2> input : register(t0);
RWStructuredBuffer<double2> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double2 d0;
	double2 d1;
	d0.x = input[sv_dispatchthreadid.x].y;
	d1 = input[sv_dispatchthreadid.x].yx;
	d0.x = d0.x + d1.y;
	d0.y = d1.x * d1.x;
	d1.x = d0.y;
	d1.y = d0.x;
	output[sv_dispatchthreadid.x] = d1;
}
