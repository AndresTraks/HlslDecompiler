half4 main(float3 texcoord : TEXCOORD) : COLOR
{
	half4 o;

	o.xy = saturate(texcoord.xy);
	o.zw = texcoord.zz * float2(1, 0) + float2(0, 1);

	return o;
}
