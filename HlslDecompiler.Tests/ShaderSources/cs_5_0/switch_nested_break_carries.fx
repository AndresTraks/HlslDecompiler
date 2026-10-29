cbuffer C : register(b0)
{
	int mode;
};

RWStructuredBuffer<float> outbuf : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = 0;
	int t1 = 0;
	while (t1 < 8) {
		float t2;
		switch (mode) {
			case 0:
				t2 = (float)t1 + t0;
				break;
			case 1:
				t2 = t0 + 100;
				break;
			case 2:
				if (t1 > 4) {
					t2 = t0;
					break;
				}
				t2 = t0 + 200;
				break;
			default:
				t2 = t0;
				break;
		}
		if ((t1 & 1) != 0) {
			t0 = t2;
			t1 = t1 + 1;
			continue;
		}
		t0 = t2;
		t1 = t1 + 1;
	}
	outbuf[sv_dispatchthreadid.x] = t0;
}
