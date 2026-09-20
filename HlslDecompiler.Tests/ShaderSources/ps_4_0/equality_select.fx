float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	float4 t0 = texcoord.x == k.x ? texcoord * k.yyyy : texcoord;
	return (texcoord.z == k.w ? k.xxxx : k.yyyy) * (texcoord.y != k.z ? t0 + k.w : t0);
}
