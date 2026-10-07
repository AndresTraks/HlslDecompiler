interface IShade
{
	float4 F0(float4 color);
	float4 F1(float4 color);
};

class A : IShade
{
	float4 F0(float4 color)
	{
		float4 r0;
		r0 = color + float4(0.25, 0.25, 0.25, 0.25);

		return r0;
	}

	float4 F1(float4 color)
	{
		float4 r1;
		r1 = color * float4(0.5, 0.5, 0.5, 0.5);

		return r1;
	}
};

class B : IShade
{
	float4 F0(float4 color)
	{
		float4 r0;
		r0 = color + float4(-0.125, -0.125, -0.125, -0.125);

		return r0;
	}

	float4 F1(float4 color)
	{
		float4 r1;
		r1 = saturate(color * float4(1.5, 1.5, 1.5, 1.5));

		return r1;
	}
};

IShade g_one;
IShade g_many[3];

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = g_one.F0(color);
	r1 = g_many[2].F1(color);
	o = r0 + r1;

	return o;
}
