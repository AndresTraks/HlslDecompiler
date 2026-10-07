float2x2 bumpEnvMat1;
sampler2D s1;

struct PS_IN
{
	float2 texcoord1 : TEXCOORD1;
	float2 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	r0.w = 0;
	r0.x = dot(i.color.xy, transpose(bumpEnvMat1)[0].xy) + r0.w;
	r0.y = dot(i.color.xy, transpose(bumpEnvMat1)[1].xy) + r0.w;
	r0.xy = r0.xy + i.texcoord1.xy;
	r0 = tex2D(s1, r0.xy);
	o = r0;

	return o;
}
