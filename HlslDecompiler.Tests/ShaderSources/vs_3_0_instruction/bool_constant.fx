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

	float4 r0;
	r0.x = flipped.x * i.position.y;
	r0.y = r0.x * -2 + i.position.y;
	r0.xzw = i.position.xzw;
	o.position.x = dot(r0, transpose(wvp)[0]);
	o.position.y = dot(r0, transpose(wvp)[1]);
	o.position.z = dot(r0, transpose(wvp)[2]);
	o.position.w = dot(r0, transpose(wvp)[3]);
	if (doubled) {
		r0 = i.color;
		[loop]
		for (int i0 = 0; i0 < repeats; i0++) {
			r0 = r0 + r0;
		}
		o.color = r0;
	} else {
		o.color = i.color;
	}

	return o;
}
