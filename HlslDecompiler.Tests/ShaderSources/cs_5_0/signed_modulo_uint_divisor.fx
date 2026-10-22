RWStructuredBuffer<int> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = sv_dispatchthreadid.x - 4;
	int t1 = (t0 ^ 4) & -2147483648 ? -((uint)abs(t0) >> 2) : (uint)abs(t0) >> 2;
	uint2 t2;
	o.GetDimensions(t2.x, t2.y);
	o[sv_dispatchthreadid.x] = t1 + ((int)(sv_dispatchthreadid.x - 4)) % (int)(t2.x);
}
