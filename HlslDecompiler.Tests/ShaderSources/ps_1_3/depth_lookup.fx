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

	float4 t0 = tex2D(s0, i.texcoord);
	float t1 = dot(i.texcoord2, t0.xyz);
	float t2 = dot(i.texcoord3, t0.xyz);
	o.depth = t1 == 0 ? 1 : dot(i.texcoord1, t0.xyz) / t1;
	o.color = t0 * tex2D(s3, float2(t2, 0));

	return o;
}
