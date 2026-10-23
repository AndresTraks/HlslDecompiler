cbuffer C : register(b0)
{
	uint2 size;
	float minLog;
	float invRange;
	float adapt;
};

Texture2D hdr;
RWStructuredBuffer<uint> hist : register(u0);
RWStructuredBuffer<float> exposure : register(u1);

groupshared int g0[256];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(16, 16, 1)]
void main(CS_IN i)
{
	g0[i.sv_groupindex] = 0;
	GroupMemoryBarrierWithGroupSync();
	int t0 = i.sv_dispatchthreadid.y < size.y;
	if (t0 && i.sv_dispatchthreadid.x < size.x) {
		t0 = 0;
		float t1 = hdr.Load(int3(i.sv_dispatchthreadid.xy, 0)).z;
		float t2 = dot(float2(0.212599993, 0.715200007), hdr.Load(int3(i.sv_dispatchthreadid.xy, 0)).xy) + 0.0722000003 * t1;
		InterlockedAdd(g0[t2 < 0.0000999999975 ? 0 : (uint)(254 * saturate((log2(t2) - minLog) * invRange) + 1)], 1);
	}
	GroupMemoryBarrierWithGroupSync();
	InterlockedAdd(hist[i.sv_groupindex], g0[i.sv_groupindex]);
	if (i.sv_groupindex == 0) {
		float t3 = 0;
		for (uint t4 = 1; t4 < 256; t4 = t4 + 1) {
			t3 = t3 + (float)(g0[t4]) * (float)t4;
		}
		float t5 = exposure[0];
		exposure[0] = lerp(t5, exp2(0.00393700786 * (t3 / max(256 - (float)(g0[0]), 1) - 1) / invRange + minLog), adapt);
	}
}
