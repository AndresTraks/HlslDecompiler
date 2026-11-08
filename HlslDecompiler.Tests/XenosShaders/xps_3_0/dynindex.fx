float4 k[16];

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return k[(int)texcoord.x] + k[(int)texcoord.y + 3];
}
