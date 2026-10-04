cbuffer Packed : register(b0)
{
	uint2 bits;
	double scale;
};

RWStructuredBuffer<float> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	double d0;
	r0 = (float2)bits.xy;
	d0 = d0 * scale;
	r0.x = (float)d0;
	output[sv_dispatchthreadid.x] = r0.x;
}
