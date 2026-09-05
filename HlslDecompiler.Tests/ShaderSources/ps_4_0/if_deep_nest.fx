float4 a;
float4 b;
float4 c;
float4 d;
float t;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	if (t < texcoord.x) {
		if (t < texcoord.y) {
			if (t < texcoord.z) {
				return a;
			} else {
				return b;
			}
		}
		return c;
	}
	return d;
}
