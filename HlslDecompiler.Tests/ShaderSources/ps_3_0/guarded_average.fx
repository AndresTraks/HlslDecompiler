float4 control : register(c8);
float4 offsets[8];
sampler2D source;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float2 t1 = 0;
	float2 t2 = float2(0, control.y);
	for (int i = 0; i < 8; i++) {
		float t10 = t1.y;
		float t9 = t1.y - 1;
		float t8 = t1.y - 2;
		float t7 = t1.y - 3;
		float t6 = t1.y - 4;
		float t5 = t1.y - 5;
		float t4 = t1.y - 6;
		float t3 = t1.y - 7;
		float t11 = t3 == 0 ? offsets[7].w : t4 == 0 ? offsets[6].w : t5 == 0 ? offsets[5].w : t6 == 0 ? offsets[4].w : t7 == 0 ? offsets[3].w : t8 == 0 ? offsets[2].w : t9 == 0 ? offsets[1].w : t10 == 0 ? offsets[0].w : 0;
		if (t11 <= 0) {
			break;
		}
		float2 t12 = (t3 == 0 ? offsets[7].xy : t4 == 0 ? offsets[6].xy : t5 == 0 ? offsets[5].xy : t6 == 0 ? offsets[4].xy : t7 == 0 ? offsets[3].xy : t8 == 0 ? offsets[2].xy : t9 == 0 ? offsets[1].xy : t10 == 0 ? offsets[0].xy : 0) * control.x + texcoord;
		float4 t13 = tex2Dlod(source, float4(t12, t2));
		t0 = t0 + t13 * t11;
		t1 = float2(t11 + t1.x, t1.y + 1);
	}
	return t0 / max(t1.x, 0.0000999999975);
}
