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
	float t0 = input[sv_dispatchthreadid.x].weight;
	output[sv_dispatchthreadid.x].weight = 2 * t0;
	double t1 = input[sv_dispatchthreadid.x].value;
	double t2 = t1 * t1;
	uint t3 = input[sv_dispatchthreadid.x].tag;
	output[sv_dispatchthreadid.x].value = t2;
	output[sv_dispatchthreadid.x].tag = t3 + 1;
}
