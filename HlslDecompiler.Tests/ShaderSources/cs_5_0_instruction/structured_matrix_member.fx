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
	float4 r0;
	float4 r1;
	float4 r2;
	r0 = transpose(bones[sv_dispatchthreadid.x].skin)[0];
	r1 = bones[sv_dispatchthreadid.x].tint;
	r0.x = dot(r1, r0);
	r2 = transpose(bones[sv_dispatchthreadid.x].skin)[1];
	r0.y = dot(r1, r2);
	r2 = transpose(bones[sv_dispatchthreadid.x].skin)[2];
	r0.z = dot(r1, r2);
	r2 = transpose(bones[sv_dispatchthreadid.x].skin)[3];
	r0.w = dot(r1, r2);
	outp[sv_dispatchthreadid.x] = r0;
}
