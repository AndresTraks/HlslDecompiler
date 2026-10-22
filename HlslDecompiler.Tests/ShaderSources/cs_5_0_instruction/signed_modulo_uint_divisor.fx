RWStructuredBuffer<int> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	uint2 dimensions0;
	o.GetDimensions(dimensions0.x, dimensions0.y);
	r0.x = dimensions0.x;
	r0.y = (int)sv_dispatchthreadid.x + -4;
	r0.xz = max(r0.xy, -(r0.xy));
	r0.x = (uint)r0.z % (uint)r0.x;
	r0.z = (uint)r0.z >> 2;
	r0.w = -r0.x;
	r1 = r0.y & -2147483648;
	r0.y = r0.y ^ 4;
	r0.y = r0.y & -2147483648;
	r0.x = (r1.x != 0) ? r0.w : r0.x;
	r0.w = -r0.z;
	r0.y = (r0.y != 0) ? r0.w : r0.z;
	r0.x = r0.y + r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
