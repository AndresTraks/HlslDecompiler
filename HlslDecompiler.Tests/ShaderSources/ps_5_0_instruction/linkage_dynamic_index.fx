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
		float3 r1;
		r1 = asfloat(r0.xyz) * asfloat(m0.xyz) + texcoord.www;

		return r1.xyz;
	}

	float3 F1(float3 normal)
	{
		int3 r2;
		r2 = asint(normal.zxy * asfloat(m0.xyz) + float3(1, 1, 1));

		return asfloat(r2.xyz);
	}
};

class B : IF
{
	float m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		float3 r1;
		r1 = asfloat(r0.zyx) * asfloat(m0) + -(texcoord.www);

		return r1.xyz;
	}

	float3 F1(float3 normal)
	{
		int3 r2;
		r2 = asint(normal.yxz * asfloat(m0) + float3(-1, -1, -1));

		return asfloat(r2.xyz);
	}
};

IF f[2];

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float3 r1;
	int3 r2;
	r0.x = asint(dot(i.normal.xyz, i.normal.xyz));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r0.xyz = asint(asfloat(r0.xxx) * i.normal.xyz);
	r0.w = which;
	r1.xyz = f[r0.w].F0(i.texcoord, asfloat(r0.xyz));
	r0.x = -(which) + 1;
	r2.xyz = asint(f[r0.x].F1(i.normal));
	o.xyz = r1.xyz + asfloat(r2.xyz);
	o.w = 1;

	return o;
}
