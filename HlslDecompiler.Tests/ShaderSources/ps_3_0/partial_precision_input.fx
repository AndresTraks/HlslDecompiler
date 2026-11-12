float4 ambient : register(c1);
float4 light;

struct PS_IN
{
	half3 texcoord1 : TEXCOORD1;
	half3 texcoord2 : TEXCOORD2;
};

half4 main(PS_IN i) : COLOR
{
	half t0 = dot((half3)light.xyz, i.texcoord1);
	return float4(i.texcoord2 * t0 + (half3)ambient.xyz, 1);
}
