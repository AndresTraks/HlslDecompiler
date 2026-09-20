StructuredBuffer<float4> source : register(t0);
AppendStructuredBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = source[sv_dispatchthreadid.x];
	if (t0.w > 0.5) {
		output.Append(t0);
	}
}
