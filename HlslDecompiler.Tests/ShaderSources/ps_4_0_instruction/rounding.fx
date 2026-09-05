float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o.x = floor(texcoord.x);
	o.y = ceil(texcoord.y);
	o.z = round(texcoord.z);
	o.w = trunc(texcoord.w);

	return o;
}
