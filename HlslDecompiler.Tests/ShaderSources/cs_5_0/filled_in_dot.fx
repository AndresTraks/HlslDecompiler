struct I
{
	float4x3 m;
	uint k;
};

StructuredBuffer<I> inst : register(t0);
RWStructuredBuffer<float3> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = inst[sv_dispatchthreadid.x].k;
	float2 t1 = float2((float)sv_dispatchthreadid.x, (float)t0);
	float4 t2 = transpose(inst[sv_dispatchthreadid.x].m)[2];
	float t3 = transpose(inst[sv_dispatchthreadid.x].m)[0].w;
	float3 t4 = transpose(inst[sv_dispatchthreadid.x].m)[0].xyz;
	float4 t5 = transpose(inst[sv_dispatchthreadid.x].m)[1];
	o[sv_dispatchthreadid.x] = float3(dot(float4(t1.x, 1, 2, t1.y), float4(t4, t3)), dot(float4(t1.x, 1, 2, t1.y), t5), dot(float4(t1.x, 1, 2, t1.y), t2));
}
