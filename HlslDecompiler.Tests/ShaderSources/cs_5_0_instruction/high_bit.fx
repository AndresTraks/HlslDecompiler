int4 k;

StructuredBuffer<int> values : register(t0);
RWStructuredBuffer<int> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.x = values[sv_dispatchthreadid.x];
	r0.y = r0.x != 0 && r0.x != -1 ? 31 - firstbithigh(r0.x) : -1;
	r0.x = r0.x + k.x;
	r0.z = (r0.y == -1) ? -1 : 0;
	r0.y = -(r0.y) + 31;
	r0.y = (r0.z != 0) ? -1 : r0.y;
	r0.z = (uint)r0.x != 0 ? 31 - firstbithigh((uint)r0.x) : -1;
	r0.z = -(r0.z) + 31;
	r0.x = (r0.x != 0) ? r0.z : -1;
	r0.x = r0.x + r0.y;
	output[sv_dispatchthreadid.x] = r0.x;
}
