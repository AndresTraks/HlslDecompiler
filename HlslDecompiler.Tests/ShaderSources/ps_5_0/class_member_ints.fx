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
		return (float)max(index >> m0.x, m0.y) * color;
	}
};

class Mask : ISel
{
	uint m0;

	float4 F0(float4 color, int index)
	{
		return index & m0 ? color : 0;
	}
};

ISel sel;

float4 main(PS_IN i) : SV_Target
{
	return sel.F0(i.color, i.index);
}
