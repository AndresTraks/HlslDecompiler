cbuffer Scene : register(b3)
{
	float4 ambient;
	float exposure;
};

interface ILight
{
	float3 F0(float3 r0);
	float3 F1(float3 normal);
};

class Sun : ILight
{
	float3 m0;
	float pad0;
	float3 m1;

	float3 F0(float3 r0)
	{
		float t0 = saturate(dot(m0, r0.xyz));
		return t0 * m1;
	}

	float3 F1(float3 normal)
	{
		float t0 = saturate(dot(m0, normal));
		return t0 * m1;
	}
};

class Point : ILight
{
	float4 m0;

	float3 F0(float3 r0)
	{
		return saturate(dot(r0.xyz, normalize(m0.xyz))) * m0.w;
	}

	float3 F1(float3 normal)
	{
		return saturate(dot(normal, normalize(m0.xyz))) * m0.w;
	}
};

ILight lights[2];

float4 main(float3 normal : NORMAL) : SV_Target
{
	return float4((lights[1].F1(normal) + lights[0].F0(normalize(normal))) * exposure.xxx, 1) + ambient;
}
