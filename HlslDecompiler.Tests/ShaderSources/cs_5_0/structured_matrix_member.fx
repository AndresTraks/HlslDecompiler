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
	float t2 = transpose(bones[sv_dispatchthreadid.x].skin)[2].w;
	float t3 = transpose(bones[sv_dispatchthreadid.x].skin)[2].z;
	float t4 = transpose(bones[sv_dispatchthreadid.x].skin)[2].y;
	float t5 = transpose(bones[sv_dispatchthreadid.x].skin)[2].x;
	float t6 = transpose(bones[sv_dispatchthreadid.x].skin)[1].w;
	float t7 = transpose(bones[sv_dispatchthreadid.x].skin)[1].z;
	float t8 = transpose(bones[sv_dispatchthreadid.x].skin)[1].y;
	float t9 = transpose(bones[sv_dispatchthreadid.x].skin)[1].x;
	float t10 = transpose(bones[sv_dispatchthreadid.x].skin)[0].w;
	float t11 = transpose(bones[sv_dispatchthreadid.x].skin)[0].z;
	float t12 = transpose(bones[sv_dispatchthreadid.x].skin)[0].y;
	float t13 = transpose(bones[sv_dispatchthreadid.x].skin)[0].x;
	outp[sv_dispatchthreadid.x] = float4(dot(t0, float4(t13, t12, t11, t10)), dot(t0, float4(t9, t8, t7, t6)), dot(t0, float4(t5, t4, t3, t2)), dot(t0, t1));
}
