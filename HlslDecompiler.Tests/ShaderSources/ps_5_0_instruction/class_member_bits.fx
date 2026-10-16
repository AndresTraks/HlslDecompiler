interface IMat
{
	float4 F0(float4 color);
};

class Plain : IMat
{
	float4 m0;

	float4 F0(float4 color)
	{
		float4 r0;
		r0 = color * m0;

		return r0;
	}
};

class Bits : IMat
{
	uint m0;
	float m1;

	float4 F0(float4 color)
	{
		int4 r2;
		float4 r0;
		r2 = asint(color) & asint(m0);
		r0 = asfloat(r2) * m1;

		return r0;
	}
};

IMat mat;

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = mat.F0(color);
	o = r0;

	return o;
}
