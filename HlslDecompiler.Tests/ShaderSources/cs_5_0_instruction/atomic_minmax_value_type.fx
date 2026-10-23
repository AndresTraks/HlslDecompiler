cbuffer C : register(b0)
{
	int v;
	uint w;
};

RWStructuredBuffer<int> si : register(u0);
RWStructuredBuffer<uint> su : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	int r2;
	int r3;
	r0.xy = (int2)sv_dispatchthreadid.xx + int2(-4, 2);
	InterlockedMin(si[0], r0.x, r1);
	r0.zw = -(sv_dispatchthreadid.xx) + int2(v, w);
	InterlockedMax(si[1], r0.z, r2);
	InterlockedMin(su[0], (uint)r0.w, r3);
	r0.z = r1.x + r2.x;
	r0.z = r3.x + r0.z;
	InterlockedMax(su[1], (uint)r0.x);
	si[r0.y] = r0.z;
}
