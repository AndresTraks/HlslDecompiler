SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return float4(tex.Sample(samp, texcoord).xy * (abs(ddy(texcoord.x)) + abs(ddx(texcoord.x))) + ddx(texcoord), tex.Sample(samp, texcoord).zw * (abs(ddy(texcoord.x)) + abs(ddx(texcoord.x))) + ddy(texcoord));
}
