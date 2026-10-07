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
	float4 t0 = tex2D(s2, i.texcoord2 + mul(tex2D(s1, i.texcoord1).xy, bumpEnvMat2));
	return t0 * i.color + tex2D(s3, i.texcoord3);
}
