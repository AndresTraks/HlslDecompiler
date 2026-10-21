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
		float3 r1;
		r0.x = saturate(dot(r0.xyz, m0.xyz));
		r1 = r0.xxx * m1.xyz;

		return r1.xyz;
	}

	float3 F1(float3 normal)
	{
		float r2;
		float3 r0;
		r2.x = saturate(dot(normal.xyz, m0.xyz));
		r0.xyz = r2.xxx * m1.xyz;

		return r0.xyz;
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

	float3 F1(float3 normal)
	{
		float3 r2;
		float3 r0;
		r2.x = dot(m0.xyz, m0.xyz);
		r2.x = rsqrt(r2.x);
		r2 = r2.xxx * m0.xyz;
		r2.x = saturate(dot(normal.xyz, r2.xyz));
		r0.z = r2.x * m0.w;
		r0.xy = r0.zz;

		return r0.xyz;
	}
};

ILight lights[2];

float4 main(float3 normal : NORMAL) : SV_Target
{
	float4 o;

	float4 r0;
	float3 r1;
	r0.x = dot(normal.xyz, normal.xyz);
	r0.x = rsqrt(r0.x);
	r0.xyz = r0.xxx * normal.xyz;
	r1.xyz = lights[0].F0(r0);
	r0.xyz = lights[1].F1(normal);
	r0.xyz = r0.xyz + r1.xyz;
	r0.xyz = r0.xyz * exposure;
	r0.w = 1;
	o = r0 + ambient;

	return o;
}
