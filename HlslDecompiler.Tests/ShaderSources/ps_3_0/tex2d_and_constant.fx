float scale;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return tex2D(tex, texcoord) * scale;
}
