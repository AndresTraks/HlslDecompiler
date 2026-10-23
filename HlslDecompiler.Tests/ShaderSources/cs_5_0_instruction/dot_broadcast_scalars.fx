StructuredBuffer<float2> s : register(t0);
RWStructuredBuffer<float> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float3 r0;
	r0.xy = s[sv_dispatchthreadid.x];
	r0.z = dot(r0.xy, float2(0.5, 0.25));
	r0.x = sqrt(r0.x);
	r0.x = dot(r0.xx, r0.zz);
	r0.x = saturate(-(r0.y) + r0.x);
	o[sv_dispatchthreadid.x] = r0.x;
}
