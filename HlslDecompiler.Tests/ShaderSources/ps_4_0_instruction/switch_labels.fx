int mode;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	switch (mode) {
		case 0:
		case 1:
		r0 = texcoord;
		break;
		case 2:
		case 3:
		case 4:
		r0 = texcoord + texcoord;
		break;
		default:
		r0 = float4(0, 0, 0, 0);
		break;
	}
	o = r0;

	return o;
}
