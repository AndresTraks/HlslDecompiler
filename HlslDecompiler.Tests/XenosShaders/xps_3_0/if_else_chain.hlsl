float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	if (k.x < t0.x) {
		t0 = 2 * t0.wxyz;
	} else {
		if ((((k.y < t0.y) ? 1 : 0) == 0 ? 1.0 : 0.0) != 0) {
			t0 = k.wxyz - t0.wxyz;
		}
	}
	return t0.yzwx;
}
