float4 k;
int steps;

SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = float4(texcoord * k.xy, texcoord + k.zw);
	float4 t1 = 0;
	[loop]
	for (int t2 = 0; t2 < steps; t2 = t2 + 1) {
		float4 t3 = tex.Sample(samp, t2 & 1 ? t0.xy : t0.zw);
		if (t3.w < 0.5) {
			break;
		}
		t1 = t3 * (float4)(t2 + 1) + t1;
	}
	return t1 / (float4)(1 + steps);
}
