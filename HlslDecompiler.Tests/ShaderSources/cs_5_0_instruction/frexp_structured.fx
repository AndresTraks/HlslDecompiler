StructuredBuffer<float> b : register(t0);
RWStructuredBuffer<float2> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	int2 r1;
	r0.x = asint(b[sv_dispatchthreadid.x]);
	r0.y = r0.x & 2139095040;
	r0.y = r0.y + -1056964608;
	r0.z = (asfloat(r0.x) != 0) ? -1 : 0;
	r0.x = (1056964608 & ~8388607) | (r0.x & 8388607);
	r1.x = r0.z & r0.x;
	r0.x = r0.z & r0.y;
	r0.x = r0.x >> 23;
	r1.y = asint((float)r0.x);
	o[sv_dispatchthreadid.x] = asfloat(r1.xy);
}
