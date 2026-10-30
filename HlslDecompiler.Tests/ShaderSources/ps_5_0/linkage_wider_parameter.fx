interface ILight
{
	float3 F0(float4 r0);
};

class Dir : ILight
{
	float3 m0;
	float pad0;
	float3 m1;

	float3 F0(float4 r0)
	{
		float t0 = saturate(dot(m0, r0.xyz));
		return t0 * m1;
	}
};

class Amb : ILight
{
	float3 m0;

	float3 F0(float4 r0)
	{
		return (0.5 * r0.y + 0.5) * m0;
	}
};

ILight g_light;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float3 t0 = normalize(normal);
	return float4(g_light.F0(float4(t0, 0)), 1);
}
