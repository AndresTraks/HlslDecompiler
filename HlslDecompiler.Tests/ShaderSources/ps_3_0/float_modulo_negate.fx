float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return k.w - fmod(texcoord, -k.z);
}
