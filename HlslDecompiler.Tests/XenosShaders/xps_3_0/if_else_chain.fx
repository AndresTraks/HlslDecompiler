float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0;
	if (texcoord.x > k.x) {
		t0 = texcoord * 2;
	} else if (texcoord.y > k.y) {
		t0 = texcoord.yzwx;
	} else {
		t0 = k - texcoord;
	}
	return t0;
}
