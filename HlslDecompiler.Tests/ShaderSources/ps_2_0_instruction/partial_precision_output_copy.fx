sampler2D normals;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	half3 texcoord1 : TEXCOORD1;
	half3 texcoord2 : TEXCOORD2;
	half3 texcoord3 : TEXCOORD3;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	half3 r1;
	r0 = tex2D(normals, i.texcoord.xy);
	r0.xyz = half3(r0.xyz * 2 + -1);
	r1 = r0.yyy * i.texcoord2.xyz;
	r1 = r0.xxx * i.texcoord1.xyz + r1.xyz;
	r0.xyz = half3(r0.zzz * i.texcoord3.xyz + r1.xyz);
	r0.w = 1;
	o = r0;

	return o;
}
