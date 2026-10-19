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
	int4 r1;
	float4 r2;
	r0 = transpose(inst[sv_dispatchthreadid.x].m)[0];
	r1.x = inst[sv_dispatchthreadid.x].k;
	r1.w = asint((float)(uint)r1.x);
	r1.x = asint((float)(uint)sv_dispatchthreadid.x);
	r1.yz = int2(1065353216, 1073741824);
	r0.x = dot(asfloat(r1), r0);
	r2 = transpose(inst[sv_dispatchthreadid.x].m)[1];
	r0.y = dot(asfloat(r1), r2);
	r2 = transpose(inst[sv_dispatchthreadid.x].m)[2];
	r0.z = dot(asfloat(r1), r2);
	o[sv_dispatchthreadid.x] = r0.xyz;
}
