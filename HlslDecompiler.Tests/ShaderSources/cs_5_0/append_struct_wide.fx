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
	float3 t0 = inp[sv_dispatchthreadid.x].pos;
	int t1 = inp[sv_dispatchthreadid.x].id;
	if ((t1 & 1) != 0) {
		t0 = 2 * t0;
		float t2 = asfloat(t1);
		P t3;
		t3.pos = t0;
		t3.id = asuint(t2);
		t3.uv = inp[sv_dispatchthreadid.x].uv.yx;
		outp.Append(t3);
	}
}
