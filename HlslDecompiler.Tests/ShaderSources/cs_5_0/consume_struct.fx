struct P
{
	float4 c;
	int n;
};

ConsumeStructuredBuffer<P> inq : register(u0);
RWStructuredBuffer<float4> o : register(u1);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	P t0 = inq.Consume();
	float t1 = t0.c.x;
	o[sv_dispatchthreadid.x] = (float)(t0.n) * float4(t1, t0.c.yzw);
}
