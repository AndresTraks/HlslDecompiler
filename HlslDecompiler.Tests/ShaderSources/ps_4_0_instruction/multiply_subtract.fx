float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	o.w = abs(texcoord.w);
	o.x = 3;
	o.yz = texcoord.xy * float2(0, 2) + float2(0, -1);

	return o;
}
