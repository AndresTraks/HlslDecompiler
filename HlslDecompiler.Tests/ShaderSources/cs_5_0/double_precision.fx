cbuffer Params : register(b0)
{
	double scale;
	double bias;
	double2 range;
};

RWBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double t0 = (double)sv_dispatchthreadid.x * scale + bias;
	output[sv_dispatchthreadid.x] = (float4)t0;
	output[sv_dispatchthreadid.x + 1] = (float4)(clamp(t0, range.x, range.y));
	output[sv_dispatchthreadid.x + 2] = t0 < range.x ? 1.0 : 0;
	output[sv_dispatchthreadid.x + 3] = (float4)(scale / bias);
}
