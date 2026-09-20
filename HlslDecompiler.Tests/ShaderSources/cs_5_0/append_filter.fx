StructuredBuffer<float4> source : register(t0);
AppendStructuredBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	if (source[sv_dispatchthreadid.x].w > 0.5) {
		output.Append(source[sv_dispatchthreadid.x]);
	}
}
