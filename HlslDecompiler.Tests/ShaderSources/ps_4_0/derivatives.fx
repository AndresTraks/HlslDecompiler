SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return tex.Sample(samp, texcoord) * (abs(ddy(texcoord.x)) + abs(ddx(texcoord.x))) + float4(ddx(texcoord), ddy(texcoord));
}
