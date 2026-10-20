cbuffer Splits : register(b0)
{
	float4 splits;
	float depth;
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

RWStructuredBuffer<int> cascade : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.x = asint((float)(uint)sv_dispatchthreadid.x);
	r0.x = asint(asfloat(r0.x) + depth);
	r0.yz = int2(0, 0);
	while (true) {
		r0.w = (r0.z >= 4) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = asint(dot(splits, icb[r0.z]));
		r0.w = (asfloat(r0.w) < asfloat(r0.x)) ? -1 : 0;
		r0.z = r0.z + 1;
		r0.y = (r0.w != 0) ? r0.z : r0.y;
	}
	cascade[sv_dispatchthreadid.x] = r0.y;
}
