cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<float> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.x = (int)sv_dispatchthreadid.x + -4;
	r0.y = asint((float)r0.x);
	r0.z = s[sv_dispatchthreadid.x];
	r0.w = asint((float)r0.z);
	r0.xz = asint((float2)(uint2)r0.xz);
	r0.y = asint(asfloat(r0.w) + asfloat(r0.y));
	r0.y = asint(asfloat(r0.z) + asfloat(r0.y));
	r0.z = (int)n + -9;
	r0.z = asint((float)r0.z);
	r0.y = asint(asfloat(r0.z) + asfloat(r0.y));
	r0.x = asint(asfloat(r0.x) + asfloat(r0.y));
	o[sv_dispatchthreadid.x] = asfloat(r0.x);
}
