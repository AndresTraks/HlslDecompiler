cbuffer c : register(b0)
{
	float4 a;
	float4 b;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return texcoord + asfloat(int4((a.x < texcoord.x) != (texcoord.x < b.x) ? 1065353216 : 0, (a.y < texcoord.y) != (texcoord.y < b.y) ? 1065353216 : 0, (a.z < texcoord.z) != (texcoord.z < b.z) ? 1065353216 : 0, (a.w < texcoord.w ? -1 : 0) != -1 ? 1065353216 : 0));
}
