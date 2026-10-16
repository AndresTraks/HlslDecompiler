globallycoherent RWStructuredBuffer<uint> g_c : register(u0);
globallycoherent RWByteAddressBuffer g_raw : register(u1);
globallycoherent RWBuffer<uint> g_typed : register(u2);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	r0.x = g_c.IncrementCounter();
	g_c[r0.x] = sv_dispatchthreadid.x;
	r0.x = g_typed[sv_dispatchthreadid.x].x;
	r0.y = sv_dispatchthreadid.x << 2;
	g_raw.Store(r0.y, r0.x);
	InterlockedAdd(g_typed[0], 1);
}
