interface ILight
{
	float3 F0(float3 r0);
};

class Sun : ILight
{
	float3 m0;
	float pad0;
	float3 m1;

	float3 F0(float3 r0)
	{
		float t0 = saturate(dot(m0, r0));
		return t0 * m1;
	}
};

class Point : ILight
{
	float4 m0;

	float3 F0(float3 r0)
	{
		return saturate(dot(r0, normalize(m0.xyz))) * m0.w;
	}
};

ILight light;

float4 main(float3 normal : NORMAL) : SV_Target
{
	return float4(light.F0(normalize(normal)), 1);
}
