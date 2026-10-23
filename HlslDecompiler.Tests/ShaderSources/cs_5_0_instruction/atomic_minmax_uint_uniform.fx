cbuffer C : register(b0)
{
	uint w;
	uint w2;
};

RWStructuredBuffer<int> si : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	r0.x = sv_dispatchthreadid.x & 1;
	r0.yw = int2(0, 0);
	InterlockedMax(si[r0.x], (int)w, r1);
	r0.z = (2 & ~(((1 << 1) - 1) << 0)) | ((sv_dispatchthreadid.x << 0) & (((1 << 1) - 1) << 0));
	InterlockedMin(si[r0.z], (int)w2);
	r0.x = sv_dispatchthreadid.x + 4;
	si[r0.x] = r1.x;
}
