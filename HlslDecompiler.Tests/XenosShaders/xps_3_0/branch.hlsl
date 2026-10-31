float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.x;
	float3 t1 = texcoord.yzw;
	float t2 = (k.x < t0) ? 1 : 0;
	if (t2 != 0) {
		if (t2 != 0) {
			float t3 = sqrt(abs(t0)) * k.x;
			t0 = sqrt(abs(t1.z)) * k.w;
			float3(t1.x = t3;, t1.yz = sqrt(abs(t1.xy)) * k.yz;)
		}
	}
	return float4(t1, t0);
}
