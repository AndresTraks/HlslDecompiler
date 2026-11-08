sampler2D s;
float4 tint;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(s, texcoord);
	return float4(t0.xy * tint.xy + float2(0.5, 0.25) * t0.w, t0.zw * tint.zw);
}
