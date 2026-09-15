float4 offsets[4];
uint count;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int2 r1;
	float4 r2;
	float4 x0[4];
	x0[0] = texcoord;
	r0 = texcoord.yzwx + offsets[1];
	x0[1] = r0;
	r0 = texcoord * offsets[2];
	x0[2] = r0;
	r0 = -(texcoord) + offsets[3];
	x0[3] = r0;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= count) ? -1 : 0;
		if (r1.y != 0) break;
		r1.y = r1.x & 3;
		r2 = x0[r1.y];
		r0 = r0 + r2;
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
