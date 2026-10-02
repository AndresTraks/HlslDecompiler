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
	int2 t0;
	switch (mode) {
		case 0:
			t0 = int2(sv_groupindex, 0);
			while (t0.y < binCount) {
				if (samples[t0.y] < 0) {
					t0 = t0 + int2(0, 1);
					continue;
				}
				t0.x = t0.y + t0.x;
				bins[t0.x] = t0.x;
				t0.y = t0.y + 1;
			}
			break;
		case 1:
			t0 = int2(sv_groupindex, 0);
			while (t0.y < binCount) {
				t0.x = t0.y + (t0.x * 2);
				bins[t0.y] = t0.x;
				t0.y = t0.y + 1;
			}
			break;
		default:
			t0.x = 0;
			break;
	}
	bins[sv_groupindex] = t0.x;
}
