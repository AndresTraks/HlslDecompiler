cbuffer Scatter : register(b0)
{
	float bias;
};

StructuredBuffer<float> scores : register(t0);
RWStructuredBuffer<uint> buckets : register(u0);

[numthreads(64, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	int2 t0;
	if (bias < scores[sv_groupindex]) {
		t0.x = 3 * sv_groupindex + 1;
		buckets[t0.x] = t0.x;
	} else {
		t0.x = 5 * sv_groupindex + 2;
		t0.y = 5 * sv_groupindex + 3;
		buckets[t0.x] = t0.y;
	}
	buckets[sv_groupindex] = t0.x;
}
