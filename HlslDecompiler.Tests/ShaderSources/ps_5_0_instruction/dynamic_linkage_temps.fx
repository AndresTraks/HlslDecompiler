interface IShade
{
	float4 F0(float4 color);
};

class Soft : IShade
{
	float4 F0(float4 color)
	{
		int4 r1;
		float4 r2;
		float4 r0;
		r1 = asint(saturate(color + color));
		r2 = color * float4(0.5, 0.5, 0.5, 0.5);
		r0 = asfloat(r1) * asfloat(r1) + r2;

		return r0;
	}
};

class Hard : IShade
{
	float4 F0(float4 color)
	{
		int r1;
		float4 r2;
		float4 r3;
		float4 r0;
		r1.x = (0.5 < color.x) ? -1 : 0;
		r2 = color * color;
		r3 = sqrt(abs(color));
		r0 = (r1.x != 0) ? r2 : r3;

		return r0;
	}
};

IShade g_shade;

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = g_shade.F0(color);
	o = r0 + r0;

	return o;
}
