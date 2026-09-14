float cutoff;
sampler2D samp;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = tex2D(samp, i.texcoord);
	clip(t0.w - cutoff >= 0 ? 0 : -1);
	return t0.w * (float4(sin(i.texcoord1), cos(i.texcoord1), ddx(i.texcoord.x), ddy(i.texcoord.y)) - t0) + t0;
}
