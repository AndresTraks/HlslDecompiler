float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int3 r1;
	float4 r2;
	r0 = texcoord * k.x;
	r1.x = (int)k.y;
	r2 = r0;
	r1.y = 1;
	while (true) {
		r1.z = (r1.y >= r1.x) ? -1 : 0;
		if (r1.z != 0) break;
		r2 = texcoord * k.x + r2;
		r1.y = r1.y + 1;
	}
	o = r2;

	return o;
}
