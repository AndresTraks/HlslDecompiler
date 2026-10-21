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
		float3 r1;
		r0.x = saturate(dot(r0.xyz, m0.xyz));
		r1 = r0.xxx * m1.xyz;

		return r1.xyz;
	}
};

class Point : ILight
{
	float4 m0;

	float3 F0(float3 r0)
	{
		float3 r2;
		float3 r1;
		r2.x = dot(m0.xyz, m0.xyz);
		r2.x = rsqrt(r2.x);
		r2 = r2.xxx * m0.xyz;
		r2.x = saturate(dot(r0.xyz, r2.xyz));
		r1.z = r2.x * m0.w;
		r1.xy = r1.zz;

		return r1.xyz;
	}
};

ILight light;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float4 o;

	float3 r0;
	float3 r1;
	r0.x = dot(normal.xyz, normal.xyz);
	r0.x = rsqrt(r0.x);
	r0 = r0.xxx * normal.xyz;
	r1.xyz = light.F0(r0);
	o.xyz = r1.xyz;
	o.w = 1;

	return o;
}
