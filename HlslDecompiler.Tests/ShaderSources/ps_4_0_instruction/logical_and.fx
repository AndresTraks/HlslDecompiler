float a;
float b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = asfloat((a < texcoord.x) ? -1 : 0);
	r0.y = asfloat((texcoord.y < b) ? -1 : 0);
	r0.x = asfloat(asint(r0.y) & asint(r0.x));
	o = (r0.x != 0) ? texcoord : -(texcoord);

	return o;
}
