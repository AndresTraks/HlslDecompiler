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
	float2 t0;
	float3 t1;
	int t2;
	if (decalCount != 0) {
		t1.xy = mul(float4(i.texcoord, 1, 1), (float4x2)decalMatrices[0]);
		if (abs(dot(transpose(decalMatrices[0])[2], float4(i.texcoord, 1, 1))) < 0.5 && all(abs(t1.xy) < 0.5)) {
			float4 t3 = decalAlbedo.Sample(linearClamp, float3(t1.xy + 0.5, 0));
			t1 = lerp(i.color.xyz, t3.xyz, t3.w);
		} else {
			t1 = i.color.xyz;
		}
		t2 = decalCount <= 1;
		if (decalCount > 1) {
			t0 = mul(float4(i.texcoord, 1, 1), (float4x2)decalMatrices[1]);
			if (abs(dot(transpose(decalMatrices[1])[2], float4(i.texcoord, 1, 1))) < 0.5 && all(abs(t0) < 0.5)) {
				float4 t4 = decalAlbedo.Sample(linearClamp, float3(t0 + 0.5, 1));
				t1 = lerp(t1, t4.xyz, t4.w);
			}
		}
	} else {
		t1 = i.color.xyz;
		t2 = -1;
	}
	float2 t5;
	if (t2 == 0) {
		if (decalCount > 2) {
			t5 = mul(float4(i.texcoord, 1, 1), (float4x2)decalMatrices[2]);
			if (abs(dot(transpose(decalMatrices[2])[2], float4(i.texcoord, 1, 1))) < 0.5 && all(abs(t5) < 0.5)) {
				float4 t6 = decalAlbedo.Sample(linearClamp, float3(t5 + 0.5, 2));
				t1 = lerp(t1, t6.xyz, t6.w);
			}
		}
	}
	return float4(t1, i.color.w);
}
