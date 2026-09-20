Texture2D source;
RWTexture2D<float4> destination : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	destination[sv_dispatchthreadid.xy] = 2 * source.Load(int3(sv_dispatchthreadid.xy, 0));
}
