int mode;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0;
	switch (mode) {
		case 0:
		case 1:
			t0 = texcoord;
			break;
		case 2:
		case 3:
		case 4:
			t0 = 2 * texcoord;
			break;
		default:
			t0 = 0;
			break;
	}
	return t0;
}
