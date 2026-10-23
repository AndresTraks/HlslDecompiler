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
	int4 r0;
	int2 r1;
	g0[i.sv_groupindex.x] = 0;
	GroupMemoryBarrierWithGroupSync();
	r0.xy = (i.sv_dispatchthreadid.xy < size.xy) ? -1 : 0;
	r0.x = r0.y & r0.x;
	if (r0.x != 0) {
		r0.xy = i.sv_dispatchthreadid.xy;
		r0.zw = int2(0, 0);
		r0.xyz = asint(hdr.Load(r0.xyz).xyz);
		r0.x = asint(dot(asfloat(r0.xyz), float3(0.212599993, 0.715200007, 0.0722000003)));
		r0.y = (asfloat(r0.x) < 0.0000999999975) ? -1 : 0;
		r0.x = asint(log2(asfloat(r0.x)));
		r0.x = asint(asfloat(r0.x) + -(minLog));
		r0.x = asint(saturate(asfloat(r0.x) * invRange));
		r0.x = asint(asfloat(r0.x) * 254 + 1);
		r0.x = (uint)asfloat(r0.x);
		r0.x = (r0.y != 0) ? 0 : r0.x;
		r0.y = 0;
		InterlockedAdd(g0[r0.x], 1);
	}
	GroupMemoryBarrierWithGroupSync();
	r0.x = g0[i.sv_groupindex.x];
	r1.x = i.sv_groupindex.x;
	r1.y = 0;
	InterlockedAdd(hist[r1.x], r0.x);
	if (i.sv_groupindex.x == 0) {
		r0.xy = int2(0, 1);
		while (true) {
			r0.z = ((uint)r0.y >= 256) ? -1 : 0;
			if (r0.z != 0) break;
			r0.z = g0[r0.y];
			r0.z = asint((float)(uint)r0.z);
			r0.w = asint((float)(uint)r0.y);
			r0.x = asint(asfloat(r0.z) * asfloat(r0.w) + asfloat(r0.x));
			r0.y = r0.y + 1;
		}
		r0.y = g0[0];
		r0.y = asint((float)(uint)r0.y);
		r0.y = asint(-(asfloat(r0.y)) + 256);
		r0.y = asint(max(asfloat(r0.y), 1));
		r0.x = asint(asfloat(r0.x) / asfloat(r0.y));
		r0.x = asint(asfloat(r0.x) + -1);
		r0.x = asint(asfloat(r0.x) * 0.00393700786);
		r0.x = asint(asfloat(r0.x) / invRange);
		r0.x = asint(asfloat(r0.x) + minLog);
		r0.x = asint(exp2(asfloat(r0.x)));
		r0.y = asint(exposure[0]);
		r0.x = asint(-(asfloat(r0.y)) + asfloat(r0.x));
		r0.x = asint(adapt * asfloat(r0.x) + asfloat(r0.y));
		exposure[0] = asfloat(r0.x);
	}
}
