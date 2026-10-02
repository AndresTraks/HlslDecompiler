cbuffer Scatter : register(b0)
{
	float bias;
};

StructuredBuffer<float> scores : register(t0);
RWStructuredBuffer<uint> buckets : register(u0);

[numthreads(64, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	float2 r0;
	r0.x = scores[sv_groupindex.x];
	r0.x = (bias < r0.x) ? -1 : 0;
	if (asint(r0.x) != 0) {
		r0.x = sv_groupindex.x * 3 + 1;
		buckets[r0.x] = r0.x;
	} else {
		r0 = sv_groupindex.xx * int2(5, 5) + int2(2, 3);
		buckets[r0.x] = r0.y;
	}
	buckets[sv_groupindex.x] = r0.x;
}
