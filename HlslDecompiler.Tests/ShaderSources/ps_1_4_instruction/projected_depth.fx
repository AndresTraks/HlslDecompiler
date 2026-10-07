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

	float4 r6;
	float2 r5;
	float3 r2;
	float4 r1;
	float4 r0;
	r6.w = 1 / i.texcoord.w;
	r6.xy = i.texcoord.xy * r6.ww;
	r5 = r6.xy;
	r6.xyz = i.texcoord3.xyw;
	r2 = r6.xyz;
	r5 = r5.xy * float2(0.5, 0.25);
	r5.y = r5.y + 2;
	r6.w = 1 / i.texcoord1.z;
	r6.xy = i.texcoord1.xy * r6.ww;
	r1 = tex2D(s1, r6.xy);
	clip(r1.xyz);
	r6.x = 1 / r5.y;
	r6.y = r5.x * r6.x;
	r6.z = abs(r5.y);
	r6.w = (-r6.z >= 0) ? 1 : r6.y;
	o.depth = r6.w;
	r0.xyz = r2.xyz * 2 + r1.xyz;
	r0.w = r1.w;
	o.color = r0;

	return o;
}
