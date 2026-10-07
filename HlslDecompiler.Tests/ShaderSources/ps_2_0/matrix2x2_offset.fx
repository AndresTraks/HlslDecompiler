float2x2 bumpEnvMat1;
sampler2D s1;

struct PS_IN
{
	float2 texcoord1 : TEXCOORD1;
	float2 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	return tex2D(s1, mul(i.color, bumpEnvMat1) + i.texcoord1);
}
