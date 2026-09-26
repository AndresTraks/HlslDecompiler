cbuffer Params : register(b0)
{
	double a;
	double b;
};

RWStructuredBuffer<float> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	double d0;
	d0 = fma(a, b, a);
	r0.x = (float)d0;
	output[sv_dispatchthreadid.x] = r0.x;
	d0 = rcp(a);
	r0.x = (float)d0;
	r0.yzw = sv_dispatchthreadid.xxx + int3(1, 2, 3);
	output[r0.y] = r0.x;
	r0.x = (b == a) ? -1 : 0;
	d0 = (asint(r0.x) != 0) ? a : b;
	r0.x = (float)d0;
	output[r0.z] = r0.x;
	r0.x = (int)a;
	d0 = (double)r0.x;
	r0.x = (float)d0;
	output[r0.w] = r0.x;
}
