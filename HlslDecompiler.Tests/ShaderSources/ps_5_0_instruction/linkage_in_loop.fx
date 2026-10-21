cbuffer C : register(b0)
{
	int n;
};

interface IF
{
	float3 F0(float3 r0);
};

class A : IF
{
	float3 m0;

	float3 F0(float3 r0)
	{
		int3 r1;
		r1 = asint(asfloat(r0.xyz) * asfloat(m0.xyz));

		return asfloat(r1.xyz);
	}
};

class B : IF
{
	float m0;

	float3 F0(float3 r0)
	{
		int3 r1;
		r1 = asint(asfloat(r0.zyx) + asfloat(m0));

		return asfloat(r1.xyz);
	}
};

IF f;

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int3 r1;
	r0.xyz = asint(texcoord.xyz);
	r0.w = 0;
	while (true) {
		r1.x = (r0.w >= n) ? -1 : 0;
		if (r1.x != 0) break;
		r1.xyz = asint(f.F0(asfloat(r0.xyz)));
		r0.xyz = asint(asfloat(r1.xyz) * float3(0.5, 0.5, 0.5));
		r0.w = r0.w + 1;
	}
	o.xyz = asfloat(r0.xyz);
	o.w = 1;

	return o;
}
