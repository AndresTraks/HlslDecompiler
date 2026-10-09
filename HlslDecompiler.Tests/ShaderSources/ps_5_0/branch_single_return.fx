float k;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = t.SampleLevel(s, texcoord, 0);
	float4 t1;
	if (k < texcoord.x) {
		float4 t2 = t.SampleLevel(s, 2 * texcoord, 1);
		t1 = t0 * t2;
	} else {
		t1 = t0;
	}
	return t1;
}
