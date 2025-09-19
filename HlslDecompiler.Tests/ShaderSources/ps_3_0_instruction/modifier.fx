struct PS_IN
{
	centroid half4 texcoord : TEXCOORD;
	centroid half texcoord2 : TEXCOORD2;
};

half4 main(PS_IN i) : COLOR
{
	half4 o;

	float4 r0;
	r0 = half4(saturate(i.texcoord));
	o = half4(r0 + i.texcoord2.x);

	return o;
}
