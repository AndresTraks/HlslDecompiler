struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = (i.idx == 3) ? -1 : 0;
	r0.x = (asint(r0.x) != 0) ? 7 : i.texcoord.y;
	r0.y = (5 < i.idx) ? -1 : 0;
	if (asint(r0.y) != 0) {
		o.xzw = i.texcoord.wyw;
		o.y = r0.x;
		return o;
	} else {
		o.xz = i.texcoord.xz;
		o.y = r0.x;
		o.w = 1;
		return o;
	}

	return o;
}
