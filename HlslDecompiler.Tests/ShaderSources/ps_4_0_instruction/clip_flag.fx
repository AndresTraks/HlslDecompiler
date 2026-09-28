cbuffer K : register(b0)
{
	bool go;
};

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	float4 o;

	if (go == 0) discard;
	o = texcoord.wzyx;

	return o;
}
