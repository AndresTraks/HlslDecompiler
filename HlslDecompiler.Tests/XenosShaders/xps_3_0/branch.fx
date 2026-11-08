float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	[branch]
	if (texcoord.x > k.x) {
		t0 = sqrt(texcoord) * k;
	} else {
		t0 = texcoord.yzwx;
	}
	return t0;
}
