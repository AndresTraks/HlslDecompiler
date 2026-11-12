float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(s0, texcoord);
	half4 t1 = t0 * k;
	return t0.x <= 0.5 ? t0 * k2 : t1;
}
