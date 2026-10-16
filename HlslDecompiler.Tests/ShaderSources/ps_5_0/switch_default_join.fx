struct PS_IN
{
	nointerpolation uint idx : IDX;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float t0;
	switch (i.idx & 7) {
		case 0:
			t0 = 0.5;
			break;
		case 1:
			t0 = 1.5;
			break;
		case 2:
			t0 = -2;
			break;
		case 3:
			t0 = 7;
			break;
		default:
			t0 = 0;
			break;
	}
	return t0 * i.texcoord;
}
