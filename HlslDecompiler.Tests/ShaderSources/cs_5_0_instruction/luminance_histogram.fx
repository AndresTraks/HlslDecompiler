float4 levels;

Texture2D source;
RWStructuredBuffer<uint> histogram : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.xy = sv_dispatchthreadid.xy;
	r0.zw = int2(0, 0);
	r0.xyz = asint(source.Load(r0.xyz).xyz);
	r0.x = asint(dot(asfloat(r0.xyz), levels.xyz));
	r0.x = asint(asfloat(r0.x) * levels.w);
	r0.x = (uint)asfloat(r0.x);
	r0.x = min(r0.x, 15);
	histogram[sv_dispatchthreadid.x] = r0.x;
}
