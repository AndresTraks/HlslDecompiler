float k;
sampler2D s;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(s, texcoord);
	clip(t0.w - k);
	return t0;
}
