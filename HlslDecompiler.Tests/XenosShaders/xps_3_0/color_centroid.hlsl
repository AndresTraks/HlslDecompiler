struct PS_IN
{
	float4 texcoord : TEXCOORD;
	centroid float4 color1 : COLOR1;
};

float4 main(PS_IN i) : COLOR
{
	return i.color1.zyxw + i.texcoord.w;
}
