float d;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o = texcoord / d.x;

	return o;
}
