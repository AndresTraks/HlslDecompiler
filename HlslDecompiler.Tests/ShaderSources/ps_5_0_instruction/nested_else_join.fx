struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int3 r0;
	float4 r1;
	float4 r2;
	r0.x = i.idx & 3;
	if (r0.x == 0) {
		r1 = i.texcoord;
	} else {
		r0.y = (r0.x == 1) ? -1 : 0;
		if (r0.y != 0) {
			r1 = i.texcoord.yzwx;
		} else {
			r1 = -(i.texcoord);
		}
	}
	r2 = r1;
	r0.y = 0;
	while (true) {
		r0.z = ((uint)r0.y >= (uint)r0.x) ? -1 : 0;
		if (r0.z != 0) break;
		r2 = r2 + float4(1, 1, 1, 1);
		r0.y = r0.y + 1;
	}
	o = r2;

	return o;
}
