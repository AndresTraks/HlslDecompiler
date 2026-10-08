cbuffer Scatter : register(b0)
{
	float bias;
};

StructuredBuffer<float> scores : register(t0);
RWStructuredBuffer<uint> buckets : register(u0);

[numthreads(64, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	int t0;
	if (bias < scores[sv_groupindex]) {
		t0 = 3 * sv_groupindex + 1;
		buckets[t0] = t0;
	} else {
		t0 = 5 * sv_groupindex + 2;
		int t1 = 5 * sv_groupindex + 3;
		buckets[t0] = t1;
	}
	buckets[sv_groupindex] = t0;
}
