float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	[branch]
	if (k.x < t0.x) {
		t0 = sqrt(abs(t0.wxyz)) * k.wxyz;
	}
	return t0.yzwx;
}
