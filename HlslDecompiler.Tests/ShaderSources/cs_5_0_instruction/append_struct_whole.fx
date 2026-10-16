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
	float3 r0;
	float4 r1;
	float4 r2;
	r1 = float4(inp[sv_dispatchthreadid.x].pos, inp[sv_dispatchthreadid.x].id);
	r2.xyz = r1.xyz + r1.xyz;
	r2.w = r1.w + 1;
	P appended0;
	appended0.pos = r2.xyz;
	appended0.id = r2.w;
	r0.yz = inp[sv_dispatchthreadid.x].uv;
	appended0.uv = r0.zy;
	outp.Append(appended0);
}
