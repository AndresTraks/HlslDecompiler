float4 colour;
int n;

struct PS_IN
{
	float4 color : COLOR;
	float texcoord : TEXCOORD;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = i.color;
	for (int i_ = 0; i_ < n; i_++) {
		t0 = colour * i.texcoord + t0;
	}
	return t0;
}
