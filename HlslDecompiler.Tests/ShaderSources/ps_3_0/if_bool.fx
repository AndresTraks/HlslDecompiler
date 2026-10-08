float4 colorA;
float4 colorB;
sampler2D tex;
bool tint : register(b1);
bool useTexture;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	if (useTexture) {
		float4 t0 = tex2D(tex, texcoord);
		if (tint) {
			return t0 * colorB;
		} else {
			return t0;
		}
	} else {
		return colorA;
	}
}
