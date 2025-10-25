float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o.w = abs(texcoord.w);
	o.xy = -(texcoord.yx);
	o.z = 2;

	return o;
}
