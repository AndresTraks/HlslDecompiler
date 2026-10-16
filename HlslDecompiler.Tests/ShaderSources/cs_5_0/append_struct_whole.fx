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
	uint t0 = inp[sv_dispatchthreadid.x].id;
	float3 t1 = inp[sv_dispatchthreadid.x].pos;
	float3 t2 = 2 * t1;
	int t3 = t0 + 1;
	P t4;
	t4.pos = t2;
	t4.id = t3;
	t4.uv = inp[sv_dispatchthreadid.x].uv.yx;
	outp.Append(t4);
}
