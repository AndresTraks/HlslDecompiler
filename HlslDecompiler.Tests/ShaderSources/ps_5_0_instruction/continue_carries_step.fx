float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int r1;
	r0.x = (1 < texcoord.x) ? -1 : 0;
	r0.yz = int2(0, 0);
	while (true) {
		r0.w = (r0.z >= 8) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = (r0.x != 0) ? 8 : r0.z;
		r1 = (asfloat(r0.y) < -100) ? -1 : 0;
		if (r1.x != 0) {
			r0.z = r0.w + 1;
			continue;
		}
		r0.y = asint(asfloat(r0.y) + texcoord.z);
		r0.z = r0.w + 1;
	}
	o = asfloat(r0.y);

	return o;
}
