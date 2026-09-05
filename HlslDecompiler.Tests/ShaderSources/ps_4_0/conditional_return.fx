float4 a;
float4 b;
float4 c;
float t;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	if (t < texcoord.x) return a;
	if (t < texcoord.y) {
		return b;
	}
	return c;
}
