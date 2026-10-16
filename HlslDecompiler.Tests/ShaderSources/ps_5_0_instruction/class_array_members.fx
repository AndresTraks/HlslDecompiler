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
		float3 r0;
		r0.z = saturate(dot(normal.xyz, m0.xyz));
		r0.xy = r0.zz;

		return r0.xyz;
	}

	float3 F1(float3 normal)
	{
		float3 r1;
		r1.z = saturate(dot(normal.xyz, m0.xyz));
		r1.xy = r1.zz;

		return r1.xyz;
	}

	float3 F2(float3 normal)
	{
		float3 r1;
		r1.z = saturate(dot(normal.xyz, m0.xyz));
		r1.xy = r1.zz;

		return r1.xyz;
	}
};

class Amb : ILight
{
	float3 m0;

	float3 F0(float3 normal)
	{
		float3 r0;
		r0 = m0.xyz;

		return r0.xyz;
	}

	float3 F1(float3 normal)
	{
		float3 r1;
		r1 = m0.xyz;

		return r1.xyz;
	}

	float3 F2(float3 normal)
	{
		float3 r1;
		r1 = m0.xyz;

		return r1.xyz;
	}
};

ILight lights[3];

float4 main(float3 normal : NORMAL) : SV_Target
{
	float4 o;

	float3 r0;
	float3 r1;
	r0.xyz = lights[0].F0(normal);
	r1.xyz = lights[1].F1(normal);
	r0 = r0.xyz + r1.xyz;
	r1.xyz = lights[2].F2(normal);
	o.xyz = r0.xyz + r1.xyz;
	o.w = 1;

	return o;
}
