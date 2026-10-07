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

	float2 t0 = float2(0.5 * (i.texcoord.x / i.texcoord.w), 0.25 * (i.texcoord.y / i.texcoord.w) + 2);
	float3 t1 = i.texcoord3.xyw;
	float4 t2 = tex2D(s1, i.texcoord1.xy / i.texcoord1.z);
	clip(t2.xyz);
	o.depth = t0.y == 0 ? 1 : t0.x / t0.y;
	o.color = float4(2 * t1 + t2.xyz, t2.w);

	return o;
}
