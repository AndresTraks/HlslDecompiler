struct struct1
{
	double low;
	double high;
};

struct InputElement
{
	struct1 items[2];
};

StructuredBuffer<InputElement> input : register(t0);
RWStructuredBuffer<double> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float3 r0;
	double d0;
	d0 = input[sv_dispatchthreadid.x].items[1].high;
	output[sv_dispatchthreadid.x] = d0;
	d0 = input[sv_dispatchthreadid.x].items[0].low;
	r0.z = sv_dispatchthreadid.x + 1;
	output[r0.z] = d0;
}
