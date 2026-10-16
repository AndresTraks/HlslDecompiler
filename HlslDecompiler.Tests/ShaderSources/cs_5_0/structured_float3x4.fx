struct I
{
	float3x4 m;
	float4 tint;
};

StructuredBuffer<I> inst : register(t0);
StructuredBuffer<float4> pos : register(t1);
RWStructuredBuffer<float4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = sv_dispatchthreadid.x >> 2;
	float2 t1 = float2(inst[t0].tint.w, inst[t0].tint.x);
	uint t2 = sv_dispatchthreadid.x >> 2;
	float4 t3 = pos[sv_dispatchthreadid.x];
	float3 t4 = t3.xyz;
	float3 t5 = transpose(inst[t2].m)[0].yxz;
	float3 t6 = transpose(inst[t2].m)[1].xzy;
	float3 t7 = transpose(inst[t2].m)[2];
	float3 t8 = transpose(inst[t2].m)[3];
	o[sv_dispatchthreadid.x] = float4((dot(float3(t5.y, t6.x, t7.x), t4) + t8.x * t3.w) * t1.y, (dot(float3(t5.x, t6.z, t7.y), t4) + t8.y * t3.w) * inst[t2].tint.y, (dot(float3(t5.z, t6.y, t7.z), t4) + t8.z * t3.w) * inst[t2].tint.z, t1.x);
}
