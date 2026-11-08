struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
	float4 texcoord4 : TEXCOORD4;
	float4 texcoord5 : TEXCOORD5;
	float4 texcoord6 : TEXCOORD6;
	float4 color1 : COLOR1;
};

float4 main(PS_IN i) : COLOR
{
	return i.texcoord * i.texcoord1 + i.texcoord2 * i.texcoord3 + i.texcoord4 * i.texcoord5 + i.texcoord6 * i.color1;
}
