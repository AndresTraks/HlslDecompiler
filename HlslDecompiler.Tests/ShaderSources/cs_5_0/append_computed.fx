AppendStructuredBuffer<float4> app : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	app.Append(float4((float)sv_dispatchthreadid.x, 1, 2, 3));
}
