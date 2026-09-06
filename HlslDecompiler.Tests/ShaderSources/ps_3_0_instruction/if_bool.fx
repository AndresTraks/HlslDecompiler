float4 colorA;
float4 colorB;
sampler2D tex;
bool tint : register(b1);
bool useTexture;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	if (useTexture) {
		r0 = tex2D(tex, texcoord.xy);
		if (tint) {
			o = r0 * colorB;
		} else {
			o = r0;
		}
	} else {
		o = colorA;
	}

	return o;
}
