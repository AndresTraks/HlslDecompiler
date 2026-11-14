float4 c;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(s0, texcoord);
	return float4(dot(c.xyz, t0.xyz), t0.zw * c.zw + t0.yx * c.yx, t0.w * c.w);
}
