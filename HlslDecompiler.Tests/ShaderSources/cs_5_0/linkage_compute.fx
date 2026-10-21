RWStructuredBuffer<float3> o : register(u0);

interface IF
{
	float3 F0(float3 r0);
};

class A : IF
{
	float3 m0;

	float3 F0(float3 r0)
	{
		return r0 * m0;
	}
};

class B : IF
{
	float m0;

	float3 F0(float3 r0)
	{
		return r0.zyx + m0;
	}
};

IF f;

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	o[sv_dispatchthreadid.x] = f.F0((float3)sv_dispatchthreadid);
}
