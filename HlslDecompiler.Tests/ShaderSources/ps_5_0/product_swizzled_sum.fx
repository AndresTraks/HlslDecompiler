float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = t.Sample(s, texcoord);
	return t0.yzwx * c.yzwx + t0 * c;
}
