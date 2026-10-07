interface I0
{
	float4 F0(float4 color);
};

interface I1
{
	float4 F0(float4 color);
};

class I0C0 : I0
{
	float4 F0(float4 color)
	{
		return color - 0.125;
	}
};

class I0C1 : I0
{
	float4 F0(float4 color)
	{
		return color + 0.25;
	}
};

class I1C2 : I1
{
	float4 F0(float4 color)
	{
		return saturate(1.5 * color);
	}
};

class I1C3 : I1
{
	float4 F0(float4 color)
	{
		return 0.5 * color;
	}
};

I0 g0;
I1 g1[3];

float4 main(float4 color : COLOR) : SV_Target
{
	return g0.F0(color) + g1[2].F0(color);
}
