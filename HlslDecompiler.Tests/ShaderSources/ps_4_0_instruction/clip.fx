float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	clip(-1);
	o = texcoord;

	return o;
}
