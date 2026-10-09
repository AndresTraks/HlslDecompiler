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
	float t2 = transpose(bones[sv_dispatchthreadid.x].skin)[0].w;
	float t3 = transpose(bones[sv_dispatchthreadid.x].skin)[0].x;
	float t4 = transpose(bones[sv_dispatchthreadid.x].skin)[0].y;
	float t5 = transpose(bones[sv_dispatchthreadid.x].skin)[0].z;
	float t6 = transpose(bones[sv_dispatchthreadid.x].skin)[1].w;
	float t7 = transpose(bones[sv_dispatchthreadid.x].skin)[1].x;
	float t8 = transpose(bones[sv_dispatchthreadid.x].skin)[1].y;
	float t9 = transpose(bones[sv_dispatchthreadid.x].skin)[1].z;
	float t10 = transpose(bones[sv_dispatchthreadid.x].skin)[2].w;
	float t11 = transpose(bones[sv_dispatchthreadid.x].skin)[2].x;
	float t12 = transpose(bones[sv_dispatchthreadid.x].skin)[2].y;
	float t13 = transpose(bones[sv_dispatchthreadid.x].skin)[2].z;
	outp[sv_dispatchthreadid.x] = float4(dot(t0, float4(t3, t4, t5, t2)), dot(t0, float4(t7, t8, t9, t6)), dot(t0, float4(t11, t12, t13, t10)), dot(t0, t1));
}
