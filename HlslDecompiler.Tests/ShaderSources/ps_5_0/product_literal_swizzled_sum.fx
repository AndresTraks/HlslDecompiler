SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = t.Sample(s, texcoord);
	return float4(0.5, 0.125, 2, 0.25) * t0.yzwx + float4(0.25, 0.5, 0.125, 2) * t0;
}
