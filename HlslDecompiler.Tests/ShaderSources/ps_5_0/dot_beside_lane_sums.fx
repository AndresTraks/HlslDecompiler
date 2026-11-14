float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = t.Sample(s, texcoord);
	return float4(dot(c.xyz, t0.xyz), t0.zw * c.zw + t0.yx * c.yx, t0.w * c.w);
}
