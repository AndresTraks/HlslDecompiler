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
	float4 r0;
	float4 r1;
	float4 r2;
	r0 = transpose(inst[sv_dispatchthreadid.x].m)[0];
	r1.x = inst[sv_dispatchthreadid.x].k;
	r1.w = (float)(uint)r1.x;
	r1.x = (float)(uint)sv_dispatchthreadid.x;
	r1.yz = float2(1, 2);
	r0.x = dot(r1, r0);
	r2 = transpose(inst[sv_dispatchthreadid.x].m)[1];
	r0.y = dot(r1, r2);
	r2 = transpose(inst[sv_dispatchthreadid.x].m)[2];
	r0.z = dot(r1, r2);
	o[sv_dispatchthreadid.x] = r0.xyz;
}
