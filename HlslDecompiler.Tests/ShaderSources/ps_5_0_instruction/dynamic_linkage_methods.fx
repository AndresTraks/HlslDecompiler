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
		float4 r0;
		r0 = color * float4(0.5, 0.5, 0.5, 0.5);

		return r0;
	}

	float4 F1(float4 color)
	{
		float4 r1;
		r1 = color + float4(-0.125, -0.125, -0.125, -0.125);

		return r1;
	}

	float4 F2(float4 color)
	{
		float4 r1;
		r1 = saturate(color * float4(3, 3, 3, 3));

		return r1;
	}
};

class Flat : IShade
{
	float4 F0(float4 color)
	{
		float4 r0;
		r0 = float4(1, 0, 0, 1);

		return r0;
	}

	float4 F1(float4 color)
	{
		float4 r1;
		r1 = color + float4(0.25, 0.25, 0.25, 0.25);

		return r1;
	}
};

class Plain : ITint
{
	float4 F2(float4 color)
	{
		float4 r1;
		r1 = color + float4(0.75, 0.75, 0.75, 0.75);

		return r1;
	}
};

IShade g_shade;
ITint g_tint[2];

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = g_shade.F0(color);
	r1 = g_shade.F1(color);
	r0 = r0 + r1;
	r1 = g_tint[1].F2(color);
	o = r0 + r1;

	return o;
}
