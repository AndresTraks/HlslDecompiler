cbuffer C : register(b0)
{
	int which;
};

struct PS_IN
{
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD;
};

interface IF
{
	float3 F0(float4 texcoord, float3 r0);
	float3 F1(float3 normal);
};

class A : IF
{
	float3 m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		return r0.xyz * m0 + texcoord.w;
	}

	float3 F1(float3 normal)
	{
		return normal.zxy * m0 + 1;
	}
};

class B : IF
{
	float m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		return r0.zyx * m0 - texcoord.w;
	}

	float3 F1(float3 normal)
	{
		return normal.yxz * m0 - 1;
	}
};

IF f[2];

float4 main(PS_IN i) : SV_Target
{
	return float4(f[which].F0(i.texcoord, normalize(i.normal)) + f[1 - which].F1(i.normal), 1);
}
