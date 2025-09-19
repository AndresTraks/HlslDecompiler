struct PS_IN
{
	centroid half4 texcoord : TEXCOORD;
	centroid half texcoord2 : TEXCOORD2;
};

half4 main(PS_IN i) : COLOR
{
	return half4(half4(saturate(i.texcoord)) + i.texcoord2);
}
