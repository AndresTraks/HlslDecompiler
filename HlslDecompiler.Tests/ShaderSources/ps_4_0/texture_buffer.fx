tbuffer Params
{
	float4 tint;
	float4 offset;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return texcoord * tint + offset;
}
