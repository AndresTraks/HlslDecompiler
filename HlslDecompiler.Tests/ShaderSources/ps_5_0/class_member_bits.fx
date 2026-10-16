interface IMat
{
	float4 F0(float4 color);
};

class Plain : IMat
{
	float4 m0;

	float4 F0(float4 color)
	{
		return color * m0;
	}
};

class Bits : IMat
{
	uint m0;
	float m1;

	float4 F0(float4 color)
	{
		return asfloat(asint(color) & m0) * m1;
	}
};

IMat mat;

float4 main(float4 color : COLOR) : SV_Target
{
	return mat.F0(color);
}
