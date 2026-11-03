sampler2D s0;
sampler2D s1;
sampler1D s2;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = float4(i.texcoord.yxx + 1.5, -0.5);
	float2 t1 = i.texcoord + float2(0.5, -0.5);
	float2 t2 = t1;
	float2 t3 = t1;
	float4 t4 = tex1D(s2, i.texcoord1 + 0.5);
	float4 t5 = tex2Dbias(s1, float4(i.texcoord.xyy, 0.5));
	float4 t6 = tex2D(s0, t3) + tex2D(s0, t2.yx);
	return float4(t4.x + tex2Dbias(s1, t0).x + t6.x * t5.x, t6.yzw * t5.yzw + tex2Dbias(s1, t0).yzw + t4.yzw);
}
