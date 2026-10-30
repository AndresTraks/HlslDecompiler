cbuffer c : register(b0)
{
	uint mode;
	int imode;
};

RWStructuredBuffer<float> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0;
	switch (sv_dispatchthreadid.x + mode) {
		case 0:
			t0 = 1;
			break;
		case 2:
			t0 = 0.5 * (float)sv_dispatchthreadid.x;
			break;
		default:
			t0 = 7;
			break;
	}
	o[sv_dispatchthreadid.x] = t0;
}
