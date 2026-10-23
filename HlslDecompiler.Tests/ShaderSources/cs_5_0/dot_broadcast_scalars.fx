StructuredBuffer<float2> s : register(t0);
RWStructuredBuffer<float> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 t1 = s[sv_dispatchthreadid.x];
	float t0 = t1.y;
	float t2 = dot(float2(t1.x, t0), float2(0.5, 0.25));
	o[sv_dispatchthreadid.x] = saturate(dot((float2)sqrt(t1.x), (float2)t2) - t0);
}
