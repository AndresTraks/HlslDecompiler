min16float4 tint;

struct PS_IN
{
	min16float4 color : COLOR;
	nointerpolation min16int i : I;
};

min16float4 main(PS_IN i) : SV_Target
{
	return i.i > 2 ? i.color * tint : 0.5 * i.color * tint;
}
