cbuffer C : register(b0)
{
	float4 lim;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int2 r1;
	r0 = texcoord;
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= 6) ? -1 : 0;
		if (r1.y != 0) break;
		r0 = r0 * float4(1.5, 1.5, 1.5, 1.5) + -(lim);
		r1.y = (lim.y < r0.x) ? -1 : 0;
		if (r1.y != 0) {
			r1.y = (r0.y < 0) ? -1 : 0;
			if (r1.y != 0) {
				o.z = (float)r1.x;
				o.w = 1;
				o.xy = r0.zw;
				return o;
			}
			r0.z = r0.z + 1;
		}
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
