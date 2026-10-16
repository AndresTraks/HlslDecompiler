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
	float4 r0;
	float4 r1;
	P consumed0 = inq.Consume();
	r1.x = consumed0.c.x;
	r1.y = consumed0.c.y;
	r1.z = consumed0.c.z;
	r1.w = consumed0.c.w;
	r0.x = consumed0.n;
	r0.x = (float)(int)r0.x;
	r0 = r0.x * r1;
	o[sv_dispatchthreadid.x] = r0;
}
