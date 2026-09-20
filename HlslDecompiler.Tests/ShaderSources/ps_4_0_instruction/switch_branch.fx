int4 k;

SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r1;
	float4 r0;
	switch (k.x) {
		case 0:
		r1.x = (1 < k.y) ? -1 : 0;
		if (asint(r1.x) != 0) {
			r0 = tex.Sample(samp, texcoord.xy);
		} else {
			r1 = tex.Sample(samp, texcoord.yx);
			r0 = r1 * float4(3, 3, 3, 3);
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
