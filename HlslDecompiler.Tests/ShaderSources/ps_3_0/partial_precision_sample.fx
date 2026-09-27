float4 k;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half4 t0 = tex2D(tex, texcoord);
	return t0 * (half4)(t0 * k.x);
}
