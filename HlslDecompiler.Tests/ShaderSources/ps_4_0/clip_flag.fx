cbuffer K : register(b0)
{
	bool go;
};

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	if (!go) {
		discard;
	}
	return texcoord.wzyx;
}
