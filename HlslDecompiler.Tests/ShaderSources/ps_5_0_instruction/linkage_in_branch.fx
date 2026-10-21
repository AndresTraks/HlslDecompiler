interface IF
{
	float3 F0(float3 texcoord);
};

class A : IF
{
	float3 m0;

	float3 F0(float3 texcoord)
	{
		int3 r0;
		r0 = asint(texcoord.yzx * m0.xyz);

		return asfloat(r0.xyz);
	}
};

class B : IF
{
	float m0;

	float3 F0(float3 texcoord)
	{
		int3 r0;
		r0 = asint(texcoord.xzy + m0);

		return asfloat(r0.xyz);
	}
};

IF f;

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int3 r0;
	r0.x = (0 < texcoord.x) ? -1 : 0;
	if (r0.x != 0) {
		r0.xyz = asint(f.F0(texcoord));
	} else {
		r0 = asint(texcoord.xyz);
	}
	o.xyz = asfloat(r0.xyz);
	o.w = 1;

	return o;
}
