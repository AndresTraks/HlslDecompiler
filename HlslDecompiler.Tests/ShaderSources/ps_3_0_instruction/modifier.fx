struct PS_IN
{
	centroid half4 texcoord : TEXCOORD;
	centroid half texcoord2 : TEXCOORD2;
};

half4 main(PS_IN i) : COLOR
{
	half4 o;

	half4 r0;
	r0 = saturate(i.texcoord);
	o = r0 + i.texcoord2.x;

	return o;
}
