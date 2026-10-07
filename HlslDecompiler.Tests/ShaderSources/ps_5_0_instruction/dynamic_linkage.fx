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
		float4 r0;
		r0 = color + float4(-0.125, -0.125, -0.125, -0.125);

		return r0;
	}
};

class I0C1 : I0
{
	float4 F0(float4 color)
	{
		float4 r0;
		r0 = color + float4(0.25, 0.25, 0.25, 0.25);

		return r0;
	}
};

class I1C2 : I1
{
	float4 F0(float4 color)
	{
		float4 r1;
		r1 = saturate(color * float4(1.5, 1.5, 1.5, 1.5));

		return r1;
	}
};

class I1C3 : I1
{
	float4 F0(float4 color)
	{
		float4 r1;
		r1 = color * float4(0.5, 0.5, 0.5, 0.5);

		return r1;
	}
};

I0 g0;
I1 g1[3];

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = g0.F0(color);
	r1 = g1[2].F0(color);
	o = r0 + r1;

	return o;
}
