float4 colour;
int n;

struct PS_IN
{
	float4 color : COLOR;
	float texcoord : TEXCOORD;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	r0 = i.color;
	for (int i0 = 0; i0 < n; i0++) {
		r0 = colour * i.texcoord.x + r0;
	}
	o = r0;

	return o;
}
