float3 cameraPos;
float3 lightDir;
float gloss;
sampler2D albedo;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	half3 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
};

half4 main(PS_IN i) : COLOR
{
	half3 t0 = normalize(cameraPos - i.texcoord);
	half t1 = 1 - saturate(dot(i.texcoord1, t0));
	half3 t2 = normalize((half3)(t0 + lightDir));
	half t3 = saturate(dot(i.texcoord1, t2));
	half t4 = pow(t3, gloss);
	return tex2D(albedo, i.texcoord2) * t1 + t4;
}
