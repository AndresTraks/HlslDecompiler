cbuffer c : register(b0)
{
	uint mode;
	int imode;
};

RWStructuredBuffer<float> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = sv_dispatchthreadid.x + mode;
	switch (r0.x) {
		case 0:
		r0.x = 1065353216;
		break;
		case 2:
		r0.y = asint((float)(uint)sv_dispatchthreadid.x);
		r0.x = asint(asfloat(r0.y) * 0.5);
		break;
		default:
		r0.x = 1088421888;
		break;
	}
	o[sv_dispatchthreadid.x] = asfloat(r0.x);
}
