struct P
{
	float3 pos;
	uint id;
	float2 uv;
};

StructuredBuffer<P> inp : register(t0);
AppendStructuredBuffer<P> outp : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int2 r1;
	r0 = int4(asint(inp[sv_dispatchthreadid.x].pos), inp[sv_dispatchthreadid.x].id);
	r1.x = r0.w & 1;
	if (r1.x != 0) {
		r1 = asint(inp[sv_dispatchthreadid.x].uv);
		r0 = asint(asfloat(r0) * float4(2, 2, 2, 1));
		P appended0;
		appended0.pos = asfloat(r0.xyz);
		appended0.id = r0.w;
		appended0.uv = asfloat(r1.yx);
		outp.Append(appended0);
	}
}
