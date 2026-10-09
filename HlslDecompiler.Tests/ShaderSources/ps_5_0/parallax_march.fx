float scale;
int steps;

SamplerState lin;
Texture2D hmap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = float3(i.texcoord1.xy / i.texcoord1.z * scale.xx, 1) / (float3)steps;
	float4 t1 = float4(0, hmap.SampleLevel(lin, i.texcoord, 0).x, i.texcoord);
	while (t1.x < t1.y) {
		t1.xzw = float3(t0.z + t1.x, t1.zw - t0.xy);
		t1.y = hmap.SampleLevel(lin, t1.zw, 0).x;
	}
	return float4(t1.zwx, 1);
}
