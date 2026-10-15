min16float4 tint;

struct PS_IN
{
	min16float4 color : COLOR;
	nointerpolation min16int i : I;
};

min16float4 main(PS_IN i) : SV_Target
{
	min16float4 o;

	int r0;
	float4 r1;
	float4 r2;
	r0 = (2 < i.i) ? -1 : 0;
	r1 = i.color * tint;
	r2 = r1 * float4(0.5, 0.5, 0.5, 0.5);
	o = (r0.x != 0) ? r1 : r2;

	return o;
}
