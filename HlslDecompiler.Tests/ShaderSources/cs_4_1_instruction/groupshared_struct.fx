struct SourceElement
{
	float3 colour;
	float weight;
};

struct ResultElement
{
	float3 colour;
	float weight;
};

StructuredBuffer<SourceElement> source : register(t0);
RWStructuredBuffer<ResultElement> result : register(u0);

groupshared float4 g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	float4 r0;
	float4 r1;
	float4 r2;
	r0 = float4(source[i.sv_dispatchthreadid.x].colour, source[i.sv_dispatchthreadid.x].weight);
	g0[i.sv_groupindex.x] = r0;
	GroupMemoryBarrierWithGroupSync();
	r1.w = 0;
	r0 = int4(0, 0, 0, 0);
	while (true) {
		r2.x = asfloat((r0.w >= 4) ? -1 : 0);
		if (asint(r2.x) != 0) break;
		r2.x = asfloat((int)r0.w + i.sv_groupindex.x);
		r2.x = asfloat(asint(r2.x) & 63);
		r2 = g0[asint(r2.x)];
		r0.xyz = r2.xyz * r2.www + r0.xyz;
		r1.w = r1.w + r2.w;
		r0.w = r0.w + 1;
	}
	r0.w = max(r1.w, 0.0000999999975);
	r1.xyz = r0.xyz / r0.www;
	result[i.sv_dispatchthreadid.x].colour = r1.xyz;
	result[i.sv_dispatchthreadid.x].weight = r1.w;
}
