interface IShade
{
	float4 F0(float4 color);
	float4 F1(float4 color);
};

class A : IShade
{
	float4 F0(float4 color)
	{
		return color + 0.25;
	}

	float4 F1(float4 color)
	{
		return 0.5 * color;
	}
};

class B : IShade
{
	float4 F0(float4 color)
	{
		return color - 0.125;
	}

	float4 F1(float4 color)
	{
		return saturate(1.5 * color);
	}
};

IShade g_one;
IShade g_many[3];

float4 main(float4 color : COLOR) : SV_Target
{
	return g_one.F0(color) + g_many[2].F1(color);
}
