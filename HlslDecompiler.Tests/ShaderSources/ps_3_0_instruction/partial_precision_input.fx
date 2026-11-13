float4 ambient : register(c1);
float4 light;

struct PS_IN
{
	half3 texcoord1 : TEXCOORD1;
	half3 texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	half r0;
	r0 = dot(i.texcoord1.xyz, light.xyz);
	o.xyz = half3(i.texcoord2.xyz * r0.xxx + ambient.xyz);
	o.w = 1;

	return o;
}
