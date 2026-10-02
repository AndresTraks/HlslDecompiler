cbuffer Histogram : register(b0)
{
	uint mode;
	uint binCount;
};

StructuredBuffer<float> samples : register(t0);
RWStructuredBuffer<uint> bins : register(u0);

[numthreads(64, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	float3 r0;
	int2 r1;
	switch (mode) {
		case 0:
		r0.x = (float)sv_groupindex.x;
		r0.y = 0;
		while (true) {
			r0.z = ((uint)r0.y >= binCount) ? -1 : 0;
			if (asint(r0.z) != 0) break;
			r0.z = samples[r0.y];
			r0.z = (r0.z < 0) ? -1 : 0;
			if (asint(r0.z) != 0) {
				r1.y = (int)r0.y + 1;
				r1.x = (int)r0.x;
				r0.xy = (float2)r1.xy;
				continue;
			}
			r0.x = r0.y + r0.x;
			bins[r0.x] = r0.x;
			r0.y = r0.y + 1;
		}
		break;
		case 1:
		r0.x = (float)sv_groupindex.x;
		r0.y = 0;
		while (true) {
			r0.z = ((uint)r0.y >= binCount) ? -1 : 0;
			if (asint(r0.z) != 0) break;
			r0.z = (int)r0.x << 1;
			r0.x = r0.y + r0.z;
			bins[r0.y] = r0.x;
			r0.y = r0.y + 1;
		}
		break;
		default:
		r0.x = 0;
		break;
	}
	bins[sv_groupindex.x] = r0.x;
}
