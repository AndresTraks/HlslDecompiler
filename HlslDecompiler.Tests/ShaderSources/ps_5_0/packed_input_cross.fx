SamplerState samp;
Texture2D tex;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float4 color : COLOR;
};

float4 main(PS_IN i) : SV_Target
{
	return tex.Sample(samp, float2(i.texcoord1.x, i.texcoord.y)) * tex.Sample(samp, float2(i.texcoord.y, i.texcoord1.x)) * i.color;
}
