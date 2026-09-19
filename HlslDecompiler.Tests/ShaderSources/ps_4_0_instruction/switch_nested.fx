int4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	switch (k.x) {
		case 0:
		switch (k.y) {
			case 1:
			r0 = texcoord;
			break;
			default:
			r0 = -(texcoord);
			break;
		}
		break;
		case 2:
		r0 = texcoord + texcoord;
		break;
		default:
		r0 = texcoord + float4(1, 1, 1, 1);
		break;
	}
	o = r0;

	return o;
}
