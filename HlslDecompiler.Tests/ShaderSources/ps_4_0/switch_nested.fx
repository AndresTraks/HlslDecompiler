int4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0;
	switch (k.x) {
		case 0:
			float4 t1;
			switch (k.y) {
				case 1:
					t1 = texcoord;
					break;
				default:
					t1 = -texcoord;
					break;
			}
			break;
		case 2:
			t0 = 2 * texcoord;
			break;
		default:
			t0 = texcoord + 1;
			break;
	}
	return t0;
}
