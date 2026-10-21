struct PS_IN
{
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD;
};

interface IF
{
	float3 F0(float4 texcoord, float3 r0);
	float3 F1(float4 texcoord, float3 r1);
};

class A : IF
{
	float3 m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		return r0 * m0 + texcoord.y;
	}

	float3 F1(float4 texcoord, float3 r1)
	{
		return r1 * m0 + texcoord.z;
	}
};

class B : IF
{
	float m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		return r0.zyx * m0 - texcoord.y;
	}

	float3 F1(float4 texcoord, float3 r1)
	{
		return r1.zyx * m0 - texcoord.z;
	}
};

IF f;
IF g;

float4 main(PS_IN i) : SV_Target
{
	return float4(g.F1(i.texcoord, f.F0(i.texcoord, i.normal * i.texcoord.x)), 1);
}
