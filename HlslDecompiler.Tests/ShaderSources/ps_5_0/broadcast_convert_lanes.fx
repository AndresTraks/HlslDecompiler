cbuffer C : register(b0)
{
	int maxSteps;
	float limit;
};

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 dir : DIR;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = length(i.dir);
	int t1 = 0;
	float t2 = 0;
	while (t1 < maxSteps) {
		float t3 = 0.100000001 * t0 + t2;
		if (limit < t3) {
			t2 = t3;
			break;
		}
		t1 = t1 + 1;
		t2 = t3;
	}
	return float4(t2 * (1 - (float)t1 / (float)maxSteps), t2 * (1 - (float)t1 / (float)maxSteps), t2 * (1 - (float)t1 / (float)maxSteps), 1);
}
