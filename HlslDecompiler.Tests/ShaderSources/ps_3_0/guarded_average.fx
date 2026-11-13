float4 control : register(c8);
float4 offsets[8];
sampler2D source;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float2 t1 = 0;
	float2 t2 = float2(0, control.y);
	for (int i = 0; i < 8; i++) {
		float t3 = offsets[t1.y].w;
		if (t3 <= 0) {
			break;
		}
		float4 t4 = tex2Dlod(source, float4(offsets[t1.y].xy * control.x + texcoord, t2));
		t0 = t0 + t4 * t3;
		t1 = float2(t3 + t1.x, t1.y + 1);
	}
	return t0 / max(t1.x, 0.0000999999975);
}
