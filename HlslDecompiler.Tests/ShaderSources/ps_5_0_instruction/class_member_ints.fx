struct PS_IN
{
	float4 color : COLOR;
	nointerpolation int index : INDEX;
};

interface ISel
{
	float4 F0(float4 color, int index);
};

class Shift : ISel
{
	int2 m0;

	float4 F0(float4 color, int index)
	{
		int4 r1;
		float4 r0;
		r1.z = index >> asint(m0.x);
		r1.x = max(r1.z, m0.y);
		r1.x = asint((float)r1.x);
		r0 = asfloat(r1.x) * color;

		return r0;
	}
};

class Mask : ISel
{
	uint m0;

	float4 F0(float4 color, int index)
	{
		int r1;
		float4 r0;
		r1.x = index & asint(m0);
		r0 = (r1.x != 0) ? color : float4(0, 0, 0, 0);

		return r0;
	}
};

ISel sel;

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = sel.F0(i.color, i.index);
	o = r0;

	return o;
}
