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
	output[sv_dispatchthreadid.x] = input[sv_dispatchthreadid.x].items[1].high;
	output[sv_dispatchthreadid.x + 1] = input[sv_dispatchthreadid.x].items[0].low;
}
