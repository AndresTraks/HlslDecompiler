interface IF
{
	float3 F0(float3 texcoord);
};

class A : IF
{
	float3 m0;

	float3 F0(float3 texcoord)
	{
		return texcoord.yzx * m0;
	}
};

class B : IF
{
	float m0;

	float3 F0(float3 texcoord)
	{
		return texcoord.xzy + m0;
	}
};

IF f;

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float3 t0;
	if (texcoord.x > 0) {
		t0 = f.F0(texcoord);
	} else {
		t0 = texcoord;
	}
	return float4(t0, 1);
}
