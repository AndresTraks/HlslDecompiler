RWBuffer<uint> u : register(u1);

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	if (i.idx == 0) {
		u[0] = asuint(i.texcoord.z);
		clip(i.texcoord.y);
		return 2 * i.texcoord;
	} else {
		return -i.texcoord;
	}
}
