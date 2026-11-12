float4 k;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 4; i++) {
		float4 t2 = tex2D(s0, t1 * k.xy + texcoord);
		t0 = t0 + t2 * k.z;
		t1 = t1 + 1;
	}
	return t0;
}
