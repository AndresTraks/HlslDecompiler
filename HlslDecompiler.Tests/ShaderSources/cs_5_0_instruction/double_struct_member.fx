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
	double2 d0;
	double d1;
	d0 = double2(input[sv_dispatchthreadid.x].high, input[sv_dispatchthreadid.x].low);
	d1 = -(d0.y) + d0.x;
	d0.x = d0.x / d0.y;
	d0.y = d0.x;
	d0.x = d1;
	output[sv_dispatchthreadid.x].low = d0.x;
	output[sv_dispatchthreadid.x].high = d0.y;
}
