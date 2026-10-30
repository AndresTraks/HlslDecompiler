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
		float3 r1;
		r0.x = saturate(dot(r0.xyz, m0.xyz));
		r1 = r0.xxx * m1.xyz;

		return r1.xyz;
	}
};

class Amb : ILight
{
	float3 m0;

	float3 F0(float4 r0)
	{
		float3 r1;
		r0.w = r0.y * 0.5 + 0.5;
		r1 = r0.www * m0.xyz;

		return r1.xyz;
	}
};

ILight g_light;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float4 o;

	float4 r0;
	float3 r1;
	r0.x = dot(normal.xyz, normal.xyz);
	r0.x = rsqrt(r0.x);
	r0.xyz = r0.xxx * normal.xyz;
	r1.xyz = g_light.F0(r0);
	o.xyz = r1.xyz;
	o.w = 1;

	return o;
}
