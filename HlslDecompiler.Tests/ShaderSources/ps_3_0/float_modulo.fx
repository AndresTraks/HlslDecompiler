float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return fmod(texcoord, k.y) + fmod(texcoord.zwxy, -k.z) + (k.w - fmod(texcoord, k.x));
}
