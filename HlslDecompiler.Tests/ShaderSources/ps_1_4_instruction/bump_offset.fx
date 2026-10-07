float2x2 bumpEnvMat2 : register(c12);
sampler2D s1 : register(s1);
sampler2D s2 : register(s2);
sampler2D s3 : register(s3);

struct PS_IN
{
	float2 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
	float2 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r1;
	float4 r2;
	float2 r6;
	float4 r3;
	float4 r0;
	r1 = tex2D(s1, i.texcoord1.xy);
	r2.xy = i.texcoord2.xy;
	r6.x = dot(r1.xy, transpose(bumpEnvMat2)[0].xy) + 0;
	r6.y = dot(r1.xy, transpose(bumpEnvMat2)[1].xy) + 0;
	r2.xy = r2.xy + r6.xy;
	r2 = tex2D(s2, r2.xy);
	r3 = tex2D(s3, i.texcoord3.xy);
	r0 = r2 * i.color;
	r0 = r0 + r3;
	o = r0;

	return o;
}
