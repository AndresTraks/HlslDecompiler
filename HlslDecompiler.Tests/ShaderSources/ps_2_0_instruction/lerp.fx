float4 a;
float4 b;
float t;

float4 main(float texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.w = saturate(texcoord.x * t.x);
	r1 = a;
	r1 = -r1 + b;
	r0 = r0.w * r1 + a;
	o = r0;

	return o;
}
