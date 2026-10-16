cbuffer C : register(b0)
{
	float4 f;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4((float)asuint(f.x), (float)asint(texcoord.y), (float)asuint(f.z) * texcoord.x, 1);
}
