bool doubled;
bool flipped : register(c4);
int repeats;
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.position = mul(float4(i.position.x, -2 * flipped * i.position.y + i.position.y, i.position.zw), wvp);
	if (doubled) {
		float4 t0 = i.color;
		for (int i_ = 0; i_ < repeats; i_++) {
			t0 = 2 * t0;
		}
		o.color = t0;
	} else {
		o.color = i.color;
	}

	return o;
}
