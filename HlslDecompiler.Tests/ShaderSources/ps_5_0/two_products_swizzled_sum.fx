float4 c;
float4 k;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = t.Sample(s, texcoord);
	return t0.yzwx * k.yzwx + t0 * c + t0.yzwx * c.yzwx;
}
