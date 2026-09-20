StructuredBuffer<float4> source : register(t0);
AppendStructuredBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	int r1;
	r0 = source[sv_dispatchthreadid.x];
	r1 = (0.5 < r0.w) ? -1 : 0;
	if (r1.x != 0) {
		output.Append(r0);
	}
}
