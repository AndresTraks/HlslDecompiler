float4x4 decalMatrices[3];
uint decalCount;

SamplerState linearClamp;
Texture2DArray decalAlbedo;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0;
	int t1;
	if (decalCount != 0) {
		float3 t2 = mul(float4(i.texcoord, 1, 1), (float4x3)decalMatrices[0]);
		if (all(abs(t2) < 0.5)) {
			float4 t3 = decalAlbedo.Sample(linearClamp, float3(t2.xy + 0.5, 0));
			t0 = lerp(i.color.xyz, t3.xyz, t3.w);
		} else {
			t0 = i.color.xyz;
		}
		t1 = decalCount <= 1;
		if (t1 == 0) {
			float3 t4 = mul(float4(i.texcoord, 1, 1), (float4x3)decalMatrices[1]);
			if (all(abs(t4) < 0.5)) {
				float4 t5 = decalAlbedo.Sample(linearClamp, float3(t4.xy + 0.5, 1));
				t0 = lerp(t0, t5.xyz, t5.w);
			}
		}
	} else {
		t0 = i.color.xyz;
		t1 = -1;
	}
	if (t1 == 0) {
		if (decalCount > 2) {
			float3 t6 = mul(float4(i.texcoord, 1, 1), (float4x3)decalMatrices[2]);
			if (all(abs(t6) < 0.5)) {
				float4 t7 = decalAlbedo.Sample(linearClamp, float3(t6.xy + 0.5, 2));
				t0 = lerp(t0, t7.xyz, t7.w);
			}
		}
	}
	return float4(t0, i.color.w);
}
