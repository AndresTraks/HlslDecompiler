sampler2D s1 : register(s1);

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float4 texcoord3 : TEXCOORD3;
	float3 texcoord1 : TEXCOORD1;
};

struct PS_OUT
{
	float4 color : COLOR;
	float depth : DEPTH;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	float2 t0 = i.texcoord.xy / i.texcoord.w;
	float2 t1 = float2(0.5 * t0.x, 0.25 * t0.y + 2);
	float3 t2 = i.texcoord3.xyw;
	float4 t3 = tex2D(s1, i.texcoord1.xy / i.texcoord1.z);
	clip(t3.xyz);
	o.color = float4(2 * t2 + t3.xyz, t3.w);
	o.depth = t1.y == 0 ? 1 : t1.x / t1.y;

	return o;
}
