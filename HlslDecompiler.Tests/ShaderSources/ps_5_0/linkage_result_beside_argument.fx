struct PS_IN
{
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD;
};

interface IF
{
	float4 F0(float3 normal, float4 r0);
};

class A : IF
{
	float3 m0;

	float4 F0(float3 normal, float4 r0)
	{
		return float4(r0.x, normal * m0 + r0.x);
	}
};

class B : IF
{
	float m0;

	float4 F0(float3 normal, float4 r0)
	{
		return float4(r0.x, normal.zyx * m0 - r0.x);
	}
};

IF f;

float4 main(PS_IN i) : SV_Target
{
	return float4(f.F0(i.normal, float4(i.texcoord.x * i.texcoord.y, 0, 0, 0)).yzw, 1);
}
