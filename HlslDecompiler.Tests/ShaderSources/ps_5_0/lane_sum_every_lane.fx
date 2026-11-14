float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return dot(c, t.Sample(s, texcoord));
}
