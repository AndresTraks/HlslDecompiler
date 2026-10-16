globallycoherent RWStructuredBuffer<uint> g_c : register(u0);
globallycoherent RWByteAddressBuffer g_raw : register(u1);
globallycoherent RWBuffer<uint> g_typed : register(u2);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	g_c[g_c.IncrementCounter()] = sv_dispatchthreadid.x;
	g_raw.Store(sv_dispatchthreadid.x * 4, g_typed[sv_dispatchthreadid.x].x);
	InterlockedAdd(g_typed[0], 1);
}
