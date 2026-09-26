cbuffer Params : register(b0)
{
	double a;
	double b;
};

RWStructuredBuffer<float> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x] = (float)fma(a, b, a);
	output[sv_dispatchthreadid.x + 1] = (float)rcp(a);
	output[sv_dispatchthreadid.x + 2] = (float)(b == a ? a : b);
	output[sv_dispatchthreadid.x + 3] = (float)(double)(int)a;
}
