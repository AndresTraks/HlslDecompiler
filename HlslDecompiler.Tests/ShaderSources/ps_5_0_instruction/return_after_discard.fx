RWBuffer<uint> u : register(u1);

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int r0;
	if (i.idx == 0) {
		o = i.texcoord + i.texcoord;
		u[0] = asuint(i.texcoord.z);
		r0 = (i.texcoord.y < 0) ? -1 : 0;
		if (r0.x != 0) discard;
		return o;
	} else {
		o = -(i.texcoord);
		return o;
	}

	return o;
}
