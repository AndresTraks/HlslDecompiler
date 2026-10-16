struct PS_IN
{
	nointerpolation uint idx : IDX;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int r0;
	r0 = i.idx & 7;
	switch (r0.x) {
		case 0:
		r0 = 1056964608;
		break;
		case 1:
		r0 = 1069547520;
		break;
		case 2:
		r0 = -1073741824;
		break;
		case 3:
		r0 = 1088421888;
		break;
		default:
		r0 = 0;
		break;
	}
	o = asfloat(r0.x) * i.texcoord;

	return o;
}
