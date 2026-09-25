struct BonesElement
{
	float4x4 skin;
	float4 tint;
};

StructuredBuffer<BonesElement> bones : register(t0);
RWStructuredBuffer<float4> outp : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = bones[sv_dispatchthreadid.x].tint;
	float4 t1 = transpose(bones[sv_dispatchthreadid.x].skin)[3];
	outp[sv_dispatchthreadid.x] = float4(dot(t0, transpose(bones[sv_dispatchthreadid.x].skin)[0]), dot(t0, transpose(bones[sv_dispatchthreadid.x].skin)[1]), dot(t0, transpose(bones[sv_dispatchthreadid.x].skin)[2]), dot(t0, t1));
}
