sampler2D s0;
sampler2D s3 : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
};

struct PS_OUT
{
	float4 color : COLOR;
	float depth : DEPTH;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	float4 r8;
	float2 r9;
	float4 r2;
	float4 r11;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r9.x = dot(i.texcoord1.xyz, r8.xyz);
	r9.y = dot(i.texcoord2.xyz, r8.xyz);
	r2.x = 1 / r9.y;
	r2.y = r9.x * r2.x;
	r2.z = abs(r9.y);
	r2.w = (-r2.z >= 0) ? 1 : r2.y;
	o.depth = r2.w;
	r2.x = dot(i.texcoord3.xyz, r8.xyz);
	r2.y = 0;
	r11 = tex2D(s3, r2.xy);
	r0 = r8 * r11;
	o.color = r0;

	return o;
}
