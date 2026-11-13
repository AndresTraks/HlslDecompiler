float4 params;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 12 && t1 < params.x; i++) {
		float4 t2 = tex2Dlod(s0, float4(t1 * params.zw + texcoord, 0, 0));
		t0 = t0 + t2;
		t1 = t1 + params.y;
	}
	return t0;
}
