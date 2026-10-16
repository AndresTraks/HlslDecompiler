interface ILight
{
	float3 F0(float3 normal);
	float3 F1(float3 normal);
	float3 F2(float3 normal);
};

class Dir : ILight
{
	float3 m0;

	float3 F0(float3 normal)
	{
		return saturate(dot(m0, normal));
	}

	float3 F1(float3 normal)
	{
		return saturate(dot(m0, normal));
	}

	float3 F2(float3 normal)
	{
		return saturate(dot(m0, normal));
	}
};

class Amb : ILight
{
	float3 m0;

	float3 F0(float3 normal)
	{
		return m0;
	}

	float3 F1(float3 normal)
	{
		return m0;
	}

	float3 F2(float3 normal)
	{
		return m0;
	}
};

ILight lights[3];

float4 main(float3 normal : NORMAL) : SV_Target
{
	return float4(lights[0].F0(normal) + lights[1].F1(normal) + lights[2].F2(normal), 1);
}
