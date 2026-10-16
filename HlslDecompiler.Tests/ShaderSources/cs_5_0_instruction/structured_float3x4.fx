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
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float4 r6;
	float4 r7;
	r0.w = 1;
	r1 = pos[sv_dispatchthreadid.x];
	r2.x = (uint)sv_dispatchthreadid.x >> 2;
	r3.xyz = transpose(inst[r2.x].m)[0].yxz;
	r4.x = r3.y;
	r5.xyz = transpose(inst[r2.x].m)[1].xzy;
	r4.y = r5.x;
	r2.yzw = transpose(inst[r2.x].m)[2];
	r4.z = r2.y;
	r6 = float4(transpose(inst[r2.x].m)[3], inst[r2.x].tint.x);
	r7.xyz = inst[r2.x].tint.yzw;
	r4.w = r6.x;
	r0.x = dot(r4, r1);
	r5.x = r3.z;
	r3.y = r5.z;
	r3.z = r2.z;
	r5.z = r2.w;
	r3.w = r6.y;
	r0.y = dot(r3, r1);
	r5.w = r6.z;
	r7.w = r6.w;
	r0.z = dot(r5, r1);
	r0 = r0 * r7.wxyz;
	o[sv_dispatchthreadid.x] = r0;
}
