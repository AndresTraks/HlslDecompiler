float4 a;
float4 b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	precise float4 r0;
	r0 = texcoord * a;
	r0 = r0 + b;
	o = r0 * texcoord.x;

	return o;
}
