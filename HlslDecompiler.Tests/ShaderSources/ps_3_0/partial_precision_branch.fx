float4 k;
float4 k3;
sampler2D s0;
sampler2D s1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(s0, texcoord);
	half4 t1;
	if (k.x > 0) {
		t1 = t0 * tex2D(s1, texcoord);
	} else {
		t1 = t0 * k3;
	}
	return t1 * k;
}
