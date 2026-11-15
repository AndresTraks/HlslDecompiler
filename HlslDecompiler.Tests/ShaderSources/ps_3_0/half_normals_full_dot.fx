float3 cameraPos;
sampler2D albedo;
sampler2D bumps;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	half3 texcoord1 : TEXCOORD1;
	half3 texcoord2 : TEXCOORD2;
	half3 texcoord3 : TEXCOORD3;
	float2 texcoord4 : TEXCOORD4;
};

half4 main(PS_IN i) : COLOR
{
	half3 t0 = normalize(cameraPos - i.texcoord);
	float3 t1 = tex2D(bumps, i.texcoord4).xyz;
	half3 t2 = 2 * t1.zxy - 1;
	half3 t3 = normalize(t2.x * i.texcoord3 + (t2.y * i.texcoord1 + t2.z * i.texcoord2));
	float t4 = saturate(dot(t3, t0));
	return t4 * tex2D(albedo, i.texcoord4);
}
