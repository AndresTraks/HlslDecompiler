StructuredBuffer<float> InputBuffer : register(t0);
RWStructuredBuffer<float> OutputBuffer : register(u0);

[numthreads(256, 1, 1)]
void main(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    float value = InputBuffer[dispatchThreadID.x];
    OutputBuffer[dispatchThreadID.x] = value * 2.0f;
}
