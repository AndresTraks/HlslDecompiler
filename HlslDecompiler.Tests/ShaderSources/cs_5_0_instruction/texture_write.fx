Texture2D source;
RWTexture2D<float4> destination : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.xy = sv_dispatchthreadid.xy;
	r0.zw = int2(0, 0);
	r0 = asint(source.Load(r0.xyz));
	r0 = asint(asfloat(r0) + asfloat(r0));
	destination[sv_dispatchthreadid.xy] = asfloat(r0);
}
