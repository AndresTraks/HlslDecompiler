sampler2D s0;
sampler2D s1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = tex2D(s0, i.texcoord);
	if (i.texcoord1 > 0.5) {
		float2 t1 = ddx(i.texcoord);
		float2 t2 = ddy(i.texcoord);
		float4 t3 = tex2Dgrad(s1, 4 * i.texcoord, 4 * t1, 4 * t2);
		return t0 * t3;
	} else {
		return t0;
	}
}
