uint n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float2 r1;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= n.x) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = r1.x << 2;
		r1.y = r1.y;
		r0 = texcoord * r1.y + r0;
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
