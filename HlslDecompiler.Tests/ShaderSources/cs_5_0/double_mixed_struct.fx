struct InputElement
{
	float weight;
	double value;
	uint tag;
};

struct OutputElement
{
	float weight;
	double value;
	uint tag;
};

StructuredBuffer<InputElement> input : register(t0);
RWStructuredBuffer<OutputElement> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x].weight = 2 * input[sv_dispatchthreadid.x].weight;
	double t0 = input[sv_dispatchthreadid.x].value;
	double t1 = t0 * t0;
	output[sv_dispatchthreadid.x].value = t1;
	output[sv_dispatchthreadid.x].tag = input[sv_dispatchthreadid.x].tag + 1;
}
