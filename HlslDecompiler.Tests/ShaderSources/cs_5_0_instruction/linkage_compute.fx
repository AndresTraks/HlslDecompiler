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
		float3 r1;
		r1 = r0.xyz * m0.xyz;

		return r1.xyz;
	}
};

class B : IF
{
	float m0;

	float3 F0(float3 r0)
	{
		float3 r1;
		r1 = r0.zyx + m0;

		return r1.xyz;
	}
};

IF f;

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float3 r0;
	float3 r1;
	r0 = (float3)(uint3)sv_dispatchthreadid.xyz;
	r1.xyz = f.F0(r0);
	o[sv_dispatchthreadid.x] = r1.xyz;
}
