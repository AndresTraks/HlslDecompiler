cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<int> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int2 r1;
	r0.x = s[sv_dispatchthreadid.x];
	r0.y = (int)sv_dispatchthreadid.x + -4;
	r0.z = r0.x ^ r0.y;
	r0.xw = max(r0.xy, -(r0.xy));
	r0.yz = r0.yz & int2(-2147483648, -2147483648);
	r0.x = (uint)r0.w / (uint)r0.x;
	r1.x = -r0.x;
	r0.x = (r0.z != 0) ? r1.x : r0.x;
	r1 = max(int2(n, k), -(int2(n, k)));
	r0.z = (uint)r0.w % (uint)r1.x;
	r0.w = -r0.z;
	r0.y = (r0.y != 0) ? r0.w : r0.z;
	r0.x = r0.x + r0.y;
	r0.y = (uint)sv_dispatchthreadid.x % (uint)r1.y;
	r0.x = r0.y + r0.x;
	r0.y = s[0];
	r0.y = r0.y + -7;
	r0.z = max(r0.y, -(r0.y));
	r0.y = r0.y ^ n;
	r0.y = r0.y & -2147483648;
	r0.z = (uint)r0.z / (uint)r1.x;
	r0.w = -r0.z;
	r0.y = (r0.y != 0) ? r0.w : r0.z;
	r0.x = r0.y + r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
