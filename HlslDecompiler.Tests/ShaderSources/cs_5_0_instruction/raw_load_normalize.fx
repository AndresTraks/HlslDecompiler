RWByteAddressBuffer buf : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	float3 r1;
	r0.x = sv_dispatchthreadid.x * 12;
	r0.yzw = buf.Load3(r0.x);
	r1 = asfloat(r0.yzw) * float3(0.5, 0.5, 0.5);
	buf.Store3(r0.x, asuint(r1.xyz));
	r0.x = asint(dot(asfloat(r0.yzw), asfloat(r0.yzw)));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r0.xyz = asint(asfloat(r0.xxx) * asfloat(r0.yzw));
	r0.w = sv_dispatchthreadid.x * 12 + 4096;
	buf.Store3(r0.w, r0.xyz);
}
