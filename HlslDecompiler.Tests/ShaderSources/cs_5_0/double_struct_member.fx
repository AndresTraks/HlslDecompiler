struct InputElement
{
	double low;
	double high;
};

struct OutputElement
{
	double low;
	double high;
};

StructuredBuffer<InputElement> input : register(t0);
RWStructuredBuffer<OutputElement> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double t0 = input[sv_dispatchthreadid.x].high - input[sv_dispatchthreadid.x].low;
	output[sv_dispatchthreadid.x].low = t0;
	output[sv_dispatchthreadid.x].high = input[sv_dispatchthreadid.x].high / input[sv_dispatchthreadid.x].low;
}
