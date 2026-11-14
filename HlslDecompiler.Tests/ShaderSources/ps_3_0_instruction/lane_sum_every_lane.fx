float4 c;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex2D(s0, texcoord.xy);
	r1 = r0.wxyz * c.wxyz;
	r1 = r1.zwxy + r1.yzwx;
	r1 = r0.zwxy * c.zwxy + r1;
	o = r0.wxyz * c.wxyz + r1;

	return o;
}
