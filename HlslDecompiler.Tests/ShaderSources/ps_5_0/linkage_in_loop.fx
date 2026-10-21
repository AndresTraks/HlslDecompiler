cbuffer C : register(b0)
{
	int n;
};

interface IF
{
	float3 F0(float3 r0);
};

class A : IF
{
	float3 m0;

	float3 F0(float3 r0)
	{
		return r0.xyz * m0;
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

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = texcoord;
	for (int t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = 0.5 * f.F0(t0);
	}
	return float4(t0, 1);
}
