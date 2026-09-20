Texture2D source;
RWTexture2D<float4> destination : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	r0.xy = (float2)sv_dispatchthreadid.xy;
	r0.zw = int2(0, 0);
	r0 = source.Load(r0.xyz);
	r0 = r0 + r0;
	destination[sv_dispatchthreadid.xy] = r0;
}
