uint count;
float4 a;

SamplerState linearClamp;
Texture2D sceneColor : register(t3);

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = sceneColor.Sample(linearClamp, texcoord).xyz;
	float t1 = sceneColor.Sample(linearClamp, texcoord).w;
	bool t2;
	if (count != 0) {
		t0.x = t0.x + a.x;
		t2 = count <= 1;
		if (!t2) {
			t0.y = t0.y + a.y;
		}
	} else {
		t2 = true;
	}
	if (!t2) {
		t0.z = t0.z + a.z;
	}
	return float4(t0, t1);
}
