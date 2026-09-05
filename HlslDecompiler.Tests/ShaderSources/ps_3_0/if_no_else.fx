float threshold;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return threshold - texcoord.x >= 0 ? texcoord : 2 * texcoord;
}
