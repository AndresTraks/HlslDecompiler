interface IShade
{
	float4 F0(float4 color);
	float4 F1(float4 color);
};

interface ITint
{
	float4 F2(float4 color);
};

class Scaled : IShade, ITint
{
	float4 F0(float4 color)
	{
		return 0.5 * color;
	}

	float4 F1(float4 color)
	{
		return color - 0.125;
	}

	float4 F2(float4 color)
	{
		return saturate(3 * color);
	}
};

class Flat : IShade
{
	float4 F0(float4 color)
	{
		return float4(1, 0, 0, 1);
	}

	float4 F1(float4 color)
	{
		return color + 0.25;
	}
};

class Plain : ITint
{
	float4 F2(float4 color)
	{
		return color + 0.75;
	}
};

IShade g_shade;
ITint g_tint[2];

float4 main(float4 color : COLOR) : SV_Target
{
	return g_shade.F0(color) + g_shade.F1(color) + g_tint[1].F2(color);
}
