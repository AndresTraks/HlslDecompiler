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
	float3 r0;
	double d0;
	float3 r1;
	double d1;
	r0.x = input[sv_dispatchthreadid.x].weight;
	r0.x = r0.x + r0.x;
	output[sv_dispatchthreadid.x].weight = r0.x;
	d1 = input[sv_dispatchthreadid.x].value;
	r1.z = input[sv_dispatchthreadid.x].tag;
	r0.z = r1.z + 1;
	d1 = d1 * d1;
	d0 = d1;
	output[sv_dispatchthreadid.x].value = d0;
	output[sv_dispatchthreadid.x].tag = r0.z;
}
