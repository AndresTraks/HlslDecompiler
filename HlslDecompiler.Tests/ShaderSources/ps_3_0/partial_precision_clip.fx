float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(s0, texcoord);
	clip(t0.w - k.x);
	return t0 * k2;
}
