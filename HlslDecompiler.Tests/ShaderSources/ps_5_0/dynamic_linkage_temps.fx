interface IShade
{
	float4 F0(float4 color);
};

class Soft : IShade
{
	float4 F0(float4 color)
	{
		return saturate(2 * color) * saturate(2 * color) + 0.5 * color;
	}
};

class Hard : IShade
{
	float4 F0(float4 color)
	{
		return color.x > 0.5 ? color * color : sqrt(abs(color));
	}
};

IShade g_shade;

float4 main(float4 color : COLOR) : SV_Target
{
	return 2 * g_shade.F0(color);
}
