cbuffer c : register(b0)
{
	int4 n;
	float4 f;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return texcoord > 0 ? float4((float)n.x, asfloat(n.y), 0.5 * (float)n.z, 0.5 * (float)n.w) : f;
}
