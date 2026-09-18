float4 levels;

Texture2D source;
RWStructuredBuffer<uint> histogram : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 t0 = source.Load(int3(sv_dispatchthreadid.xy, 0)).yz;
	histogram[sv_dispatchthreadid.x] = min((uint)((source.Load(int3(sv_dispatchthreadid.xy, 0)).x * levels.x + t0.x * levels.y + t0.y * levels.z) * levels.w), 15);
}
