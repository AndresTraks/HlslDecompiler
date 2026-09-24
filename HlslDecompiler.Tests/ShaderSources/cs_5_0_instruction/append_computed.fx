AppendStructuredBuffer<float4> app : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float r0;
	float4 r1;
	r1.x = (float)(uint)sv_dispatchthreadid.x;
	r1.yzw = float3(1, 2, 3);
	app.Append(r1);
}
