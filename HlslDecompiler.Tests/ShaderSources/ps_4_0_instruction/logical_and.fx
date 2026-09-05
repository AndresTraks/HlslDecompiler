float a;
float b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = (a.x < texcoord.x) ? -1 : 0;
	r0.y = (texcoord.y < a.y) ? -1 : 0;
	r0.x = r0.y & r0.x;
	o = (r0.x != 0) ? texcoord : -(texcoord);

	return o;
}
