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
	return float4(t0.w * (sin(i.texcoord1) - t0.x) + t0.x, t0.w * (cos(i.texcoord1) - t0.y) + t0.y, t0.w * (ddx(i.texcoord.x) - t0.z) + t0.z, t0.w * (ddy(i.texcoord.y) - t0.w) + t0.w);
}
