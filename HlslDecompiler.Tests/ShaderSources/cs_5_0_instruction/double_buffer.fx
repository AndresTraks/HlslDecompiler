cbuffer Params : register(b0)
{
	double scale;
	double bias;
};

StructuredBuffer<double> input : register(t0);
RWStructuredBuffer<double> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double d0;
	d0 = input[sv_dispatchthreadid.x];
	d0 = d0 * scale;
	d0 = d0 + bias;
	output[sv_dispatchthreadid.x] = d0;
}
