float4 a;
int n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float3 r0;
	r0.xy = float2(0, 0);
	while (true) {
		r0.z = (r0.y >= n) ? -1 : 0;
		if (r0.z != 0) break;
		r0.x = r0.x + texcoord.x;
		r0.y = r0.y + 1;
	}
	r0.x = (r0.x < 0) ? -1 : 0;
	clip(r0.x);
	o = a;

	return o;
}
