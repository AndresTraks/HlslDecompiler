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
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex.Sample(samp, float2(i.texcoord1.x, i.texcoord.y));
	r1 = tex.Sample(samp, float2(i.texcoord.y, i.texcoord1.x));
	r0 = r0 * r1;
	o = r0 * i.color;

	return o;
}
