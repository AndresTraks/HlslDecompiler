int4 k;

SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0;
	switch (k.x) {
		case 0:
			if (k.y > 1) {
				t0 = tex.Sample(samp, texcoord.xy);
			} else {
				t0 = 3 * tex.Sample(samp, texcoord.yx);
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
