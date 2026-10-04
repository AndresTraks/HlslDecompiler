cbuffer Packed : register(b0)
{
	uint2 bits;
	double scale;
};

RWStructuredBuffer<float> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	output[sv_dispatchthreadid.x] = (float)(asdouble(bits.x, bits.y) * scale);
}
