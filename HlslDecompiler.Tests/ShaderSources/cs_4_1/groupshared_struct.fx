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
	g0[i.sv_groupindex] = float4(source[i.sv_dispatchthreadid.x].colour, source[i.sv_dispatchthreadid.x].weight);
	GroupMemoryBarrierWithGroupSync();
	float3 t0 = 0;
	float t1 = 0;
	for (int t2 = 0; t2 < 4; t2 = t2 + 1) {
		int t3 = t2 + i.sv_groupindex & 63;
		t0 = g0[t3].xyz * g0[t3].w + t0;
		t1 = t1 + g0[t3].w;
	}
	result[i.sv_dispatchthreadid.x].colour = t0 / max(t1, 0.0000999999975);
	result[i.sv_dispatchthreadid.x].weight = t1;
}
