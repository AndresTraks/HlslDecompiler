int mode;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	switch (mode.x) {
		case 0:
		r0 = texcoord;
		break;
		case 1:
		r0 = texcoord + texcoord;
		break;
		default:
		r0 = float4(1, 1, 1, 1);
		break;
	}
	o = r0;

	return o;
}
