SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = ddx(texcoord);
	float2 t1 = ddy(texcoord);
	return tex.Sample(samp, texcoord) * (abs(t0.x) + abs(t1.x)) + float4(t0, t1);
}
