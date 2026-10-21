struct PS_IN
{
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD;
};

interface IF
{
	float3 F0(float4 texcoord, float3 r0);
	float3 F1(float4 texcoord, float3 r1);
};

class A : IF
{
	float3 m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		float3 r1;
		r1 = r0.xyz * m0.xyz + texcoord.yyy;

		return r1.xyz;
	}

	float3 F1(float4 texcoord, float3 r1)
	{
		float3 r0;
		r0 = r1.xyz * m0.xyz + texcoord.zzz;

		return r0.xyz;
	}
};

class B : IF
{
	float m0;

	float3 F0(float4 texcoord, float3 r0)
	{
		float3 r1;
		r1 = r0.zyx * m0 + -(texcoord.yyy);

		return r1.xyz;
	}

	float3 F1(float4 texcoord, float3 r1)
	{
		float3 r0;
		r0 = r1.zyx * m0 + -(texcoord.zzz);

		return r0.xyz;
	}
};

IF f;
IF g;

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float3 r0;
	float3 r1;
	r0 = i.normal.xyz * i.texcoord.xxx;
	r1.xyz = f.F0(i.texcoord, r0);
	r0.xyz = g.F1(i.texcoord, r1);
	o.xyz = r0.xyz;
	o.w = 1;

	return o;
}
